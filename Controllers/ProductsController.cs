using Microsoft.AspNetCore.Mvc;
using ABCRetail.Models;
using ABCRetail.Services;

namespace ABCRetail.Controllers;

public class ProductsController : Controller
{
    private readonly AzureStorageService _storage;

    public ProductsController(AzureStorageService storage)
    {
        _storage = storage;
    }

    // Display all products
    public async Task<IActionResult> Index()
    {
        try
        {
            var products = await _storage.GetProductsAsync();

            return View(products);
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;

            return View(new List<Product>());
        }
    }

    // Show Add Product page
    [HttpGet]
    public IActionResult Create()
    {
        return View(new Product());
    }

    // Create product AND upload its image
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Product product,
        IFormFile? image)
    {
        if (!ModelState.IsValid)
        {
            return View(product);
        }

        // Make sure the product has an ID
        if (string.IsNullOrWhiteSpace(product.ProductId))
        {
            product.ProductId = $"P{DateTime.UtcNow.Ticks}";
        }

        // Upload image if one was selected
        if (image != null && image.Length > 0)
        {
            // Save the filename in the Product table
            product.ImageName = Path.GetFileName(image.FileName);

            // Upload the actual image to Blob Storage
            using var stream = image.OpenReadStream();

            await _storage.UploadBlobAsync(
                stream,
                product.ImageName,
                image.ContentType);
        }

        // Save product information to Azure Table Storage
        await _storage.AddProductAsync(product);

        TempData["Message"] = "Product and image saved successfully.";

        return RedirectToAction(nameof(Index));
    }

    // Display a specific image from Azure Blob Storage
    public async Task<IActionResult> Image(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return NotFound();
        }

        var image = await _storage.DownloadBlobAsync(fileName);

        if (image == null)
        {
            return NotFound();
        }

        return File(
            image.Content,
            image.ContentType,
            enableRangeProcessing: true);
    }

    // Download an image from Azure Blob Storage
    public async Task<IActionResult> DownloadImage(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return NotFound();
        }

        var image = await _storage.DownloadBlobAsync(fileName);

        if (image == null)
        {
            return NotFound();
        }

        return File(
            image.Content,
            image.ContentType,
            fileName);
    }
}