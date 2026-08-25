using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Models;

public class Product : ITableEntity
{
    public string PartitionKey { get; set; } = "Products";

    public string RowKey { get; set; } = Guid.NewGuid().ToString();

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string ProductId { get; set; } = "";

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public double Price { get; set; }

    public string ImageName { get; set; } = "";
}