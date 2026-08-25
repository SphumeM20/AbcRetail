namespace ABCRetail.Models;

public class OrderViewModel
{
    public string CustomerId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; } = 1;
}
