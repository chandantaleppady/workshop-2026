using Microsoft.AspNetCore.Mvc;
using MyShoppingCart.Services;

namespace MyShoppingCart.Controllers;

public class ShopController : Controller
{
    private readonly CartService _cart;

    public ShopController(CartService cart)
    {
        _cart = cart;
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
}
