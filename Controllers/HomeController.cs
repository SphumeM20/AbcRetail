using Microsoft.AspNetCore.Mvc;
using ABCRetail.Services;

namespace ABCRetail.Controllers;

public class HomeController : Controller
{
    private readonly AzureStorageService _storage;

    public HomeController(AzureStorageService storage)
    {
        _storage = storage;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            ViewBag.CustomerCount = (await _storage.GetCustomersAsync()).Count;
            ViewBag.ProductCount = (await _storage.GetProductsAsync()).Count;
            ViewBag.ImageCount = (await _storage.GetBlobNamesAsync()).Count;
            ViewBag.QueueCount = await _storage.GetQueueMessageCountAsync();
        }
        catch
        {
            ViewBag.CustomerCount = 0;
            ViewBag.ProductCount = 0;
            ViewBag.ImageCount = 0;
            ViewBag.QueueCount = 0;
            ViewBag.NotConfigured = true;
        }

        return View();
    }

    public IActionResult Privacy() => View();
}
