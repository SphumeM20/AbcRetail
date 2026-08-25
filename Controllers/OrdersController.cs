using Microsoft.AspNetCore.Mvc;
using ABCRetail.Models;
using ABCRetail.Services;

namespace ABCRetail.Controllers;

public class OrdersController : Controller
{
    private readonly AzureStorageService _storage;

    public OrdersController(AzureStorageService storage)
    {
        _storage = storage;
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Customers = await _storage.GetCustomersAsync();
        ViewBag.Products = await _storage.GetProductsAsync();
        return View(new OrderViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrderViewModel order)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerId) ||
            string.IsNullOrWhiteSpace(order.ProductId) ||
            order.Quantity < 1)
        {
            ModelState.AddModelError("", "Please select a customer, product and valid quantity.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Customers = await _storage.GetCustomersAsync();
            ViewBag.Products = await _storage.GetProductsAsync();
            return View(order);
        }

        await _storage.AddOrderToQueueAsync(order);
        TempData["Message"] = "Order added to Azure Queue Storage successfully.";
        return RedirectToAction(nameof(Create));
    }
}
