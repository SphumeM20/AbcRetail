using ABCRetail.Models;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using System.Text.Json;

namespace ABCRetail.Services;

public class AzureStorageService
{
    private readonly IConfiguration _configuration;

    public AzureStorageService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string ConnectionString => _configuration["AzureStorage:ConnectionString"]
                            ?? Environment.GetEnvironmentVariable("AzureStorage__ConnectionString")
                            ?? "";

    private void EnsureConnection()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException(
                "Azure Storage connection string is missing.");
    }

    private TableClient CustomersTable()
    {
        EnsureConnection();
        var service = new TableServiceClient(ConnectionString);
        var table = service.GetTableClient(_configuration["AzureStorage:CustomersTable"] ?? "Customers");
        table.CreateIfNotExists();
        return table;
    }

    private TableClient ProductsTable()
    {
        EnsureConnection();
        var service = new TableServiceClient(ConnectionString);
        var table = service.GetTableClient(_configuration["AzureStorage:ProductsTable"] ?? "Products");
        table.CreateIfNotExists();
        return table;
    }

    public async Task<List<Customer>> GetCustomersAsync()
    {
        var table = CustomersTable();
        var result = new List<Customer>();
        await foreach (var item in table.QueryAsync<Customer>())
            result.Add(item);
        return result.OrderBy(x => x.CustomerId).ToList();
    }

    public async Task AddCustomerAsync(Customer customer)
    {
        customer.PartitionKey = "Customers";
        customer.RowKey = string.IsNullOrWhiteSpace(customer.RowKey) ? Guid.NewGuid().ToString() : customer.RowKey;
        customer.CustomerId = string.IsNullOrWhiteSpace(customer.CustomerId) ? $"C{DateTime.UtcNow.Ticks}" : customer.CustomerId;
        await CustomersTable().UpsertEntityAsync(customer);
        await WriteLogAsync($"Customer created: {customer.CustomerId} - {customer.Name}");
    }

    public async Task<List<Product>> GetProductsAsync()
    {
        var table = ProductsTable();
        var result = new List<Product>();
        await foreach (var item in table.QueryAsync<Product>())
            result.Add(item);
        return result.OrderBy(x => x.ProductId).ToList();
    }

    public async Task AddProductAsync(Product product)
    {
        product.PartitionKey = "Products";
        product.RowKey = string.IsNullOrWhiteSpace(product.RowKey) ? Guid.NewGuid().ToString() : product.RowKey;
        product.ProductId = string.IsNullOrWhiteSpace(product.ProductId) ? $"P{DateTime.UtcNow.Ticks}" : product.ProductId;
        await ProductsTable().UpsertEntityAsync(product);
        await WriteLogAsync($"Product created: {product.ProductId} - {product.Name}");
    }



    public async Task<string> UploadBlobAsync(
       Stream stream,
       string fileName,
       string contentType)
    {
        EnsureConnection();

        var service = new BlobServiceClient(ConnectionString);

        var container = service.GetBlobContainerClient(
            _configuration["AzureStorage:ProductImagesContainer"]
            ?? "product-images");

        await container.CreateIfNotExistsAsync();

        var safeFileName = Path.GetFileName(fileName);

        var blob = container.GetBlobClient(safeFileName);

        await blob.UploadAsync(
            stream,
            overwrite: true);

        await blob.SetHttpHeadersAsync(
            new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = contentType
            });

        await WriteLogAsync(
            $"Image uploaded: {safeFileName}");

        return blob.Uri.ToString();
    }

    public async Task<List<string>> GetBlobNamesAsync()
    {
        EnsureConnection();
        var service = new BlobServiceClient(ConnectionString);
        var container = service.GetBlobContainerClient(_configuration["AzureStorage:ProductImagesContainer"] ?? "product-images");
        await container.CreateIfNotExistsAsync();

        var names = new List<string>();
        await foreach (var blob in container.GetBlobsAsync())
            names.Add(blob.Name);
        return names.OrderBy(x => x).ToList();
    }

    public async Task AddOrderToQueueAsync(OrderViewModel order)
    {
        EnsureConnection();
        var service = new QueueServiceClient(ConnectionString);
        var queue = service.GetQueueClient(_configuration["AzureStorage:OrderQueue"] ?? "order-processing");
        await queue.CreateIfNotExistsAsync();

        var orderMessage = new
        {
            OrderId = $"ORD{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            order.CustomerId,
            order.ProductId,
            order.Quantity,
            Status = "Processing",
            CreatedUtc = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(orderMessage);
        await queue.SendMessageAsync(json);
        await WriteLogAsync($"Order queued: {orderMessage.OrderId}");
    }

    public async Task<int> GetQueueMessageCountAsync()
    {
        EnsureConnection();
        var service = new QueueServiceClient(ConnectionString);
        var queue = service.GetQueueClient(_configuration["AzureStorage:OrderQueue"] ?? "order-processing");
        await queue.CreateIfNotExistsAsync();
        var properties = await queue.GetPropertiesAsync();
        return properties.Value.ApproximateMessagesCount;
    }

    public async Task WriteLogAsync(string message)
    {
        EnsureConnection();
        var service = new ShareServiceClient(ConnectionString);
        var share = service.GetShareClient(_configuration["AzureStorage:LogShare"] ?? "application-logs");
        await share.CreateIfNotExistsAsync();

        var directory = share.GetRootDirectoryClient();
        var fileName = _configuration["AzureStorage:LogFile"] ?? "application-log.txt";
        var file = directory.GetFileClient(fileName);

        var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC - {message}{Environment.NewLine}";
        string existing = "";

        try
        {
            var download = await file.DownloadAsync();
            using var reader = new StreamReader(download.Value.Content);
            existing = await reader.ReadToEndAsync();
        }
        catch
        {
            // File does not exist yet.
        }

        var content = existing + line;
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await file.CreateAsync(ms.Length);
        await file.UploadRangeAsync(new Azure.HttpRange(0, ms.Length), ms);
    }

    public async Task<BlobDownloadResult?> DownloadBlobAsync(
    string fileName)
    {
        EnsureConnection();

        var service = new BlobServiceClient(ConnectionString);
        var container = service.GetBlobContainerClient(
            _configuration["AzureStorage:ProductImagesContainer"] ?? "product-images");

        var safeFileName = Path.GetFileName(fileName);
        var blob = container.GetBlobClient(safeFileName);

        if (!await blob.ExistsAsync())
        {
            return null;
        }

        var download = await blob.DownloadStreamingAsync();
        var contentType = download.Value.Details.ContentType ?? GetContentType(fileName);

        return new BlobDownloadResult
        {
            Content = download.Value.Content,
            ContentType = contentType
        };
    }

    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }

    public class BlobDownloadResult
    {
        public Stream Content { get; set; } = Stream.Null;
        public string ContentType { get; set; } = "application/octet-stream";
    }
}
