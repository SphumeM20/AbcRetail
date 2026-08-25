using Microsoft.AspNetCore.Mvc;
using ABCRetail.Models;
using ABCRetail.Services;

namespace ABCRetail.Controllers;

public class CustomersController : Controller
{
    private readonly AzureStorageService _storage;

    public CustomersController(AzureStorageService storage)
    {
        _storage = storage;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            return View(await _storage.GetCustomersAsync());
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            return View(new List<Customer>());
        }
    }

    [HttpGet]
    public IActionResult Create() => View(new Customer());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        if (!ModelState.IsValid) return View(customer);
        await _storage.AddCustomerAsync(customer);
        return RedirectToAction(nameof(Index));
    }

   
}
