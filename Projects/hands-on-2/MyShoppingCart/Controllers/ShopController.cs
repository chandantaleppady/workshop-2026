using Microsoft.AspNetCore.Mvc;
using MyShoppingCart.Services;

namespace MyShoppingCart.Controllers;

public class ShopController : Controller
{
    private readonly CartService _cart;
    private readonly OrderService _orders;
    private readonly ILogger<ShopController> _logger;

    public ShopController(CartService cart, OrderService orders, ILogger<ShopController> logger)
    {
        _cart = cart;
        _orders = orders;
        _logger = logger;
    }

    // GET: /  (landing page - product listing)
    public IActionResult Index()
    {
        _cart.Clear(); // demo: cart always starts empty on landing page load
        ViewData["Title"] = "My Shopping Cart";
        var products = CartService.Products
            .Select(p => (Product: p, Quantity: _cart.GetQuantity(p.Id)))
            .ToList();
        return View(products);
    }

    // POST: /Shop/Add/5
    [HttpPost]
    public IActionResult Add(int id)
    {
        var itemQty = _cart.AddItem(id);
        return Json(new { count = _cart.GetTotalCount(), itemQty });
    }

    // POST: /Shop/Remove/5
    [HttpPost]
    public IActionResult Remove(int id)
    {
        var itemQty = _cart.RemoveItem(id);
        return Json(new { count = _cart.GetTotalCount(), itemQty });
    }

    // GET: /Shop/Cart  (cart drawer partial)
    [HttpGet]
    public IActionResult Cart()
    {
        return PartialView("_CartDrawer", _cart.GetCart());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmOrder()
    {
        var cart = _cart.GetCart();
        if (cart.Lines.Count == 0)
        {
            return BadRequest(new { message = "Your cart is empty." });
        }

        try
        {
            var orderId = await _orders.SaveOrderAsync(cart);
            _cart.Clear();
            return Json(new { orderId });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to save the confirmed order.");
            return StatusCode(500, new { message = "The order could not be saved. Check the database connection and try again." });
        }
    }
}
