using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Models;

public class Customer : ITableEntity
{
    public string PartitionKey { get; set; } = "Customers";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string CustomerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
}
