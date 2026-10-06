using System.Collections.Concurrent;
using MyShoppingCart.Models;

namespace MyShoppingCart.Services;

/// <summary>
/// In-memory cart model (demo only - no persistence).
/// Holds the static product catalog and the current cart state.
/// </summary>
public class CartService
{
    private readonly ConcurrentDictionary<int, int> _cart = new();

    private const string Img = "https://images.unsplash.com/photo-";

    public static readonly IReadOnlyList<Product> Products = new List<Product>
    {
        new() { Id = 1, Name = "Tomato",      Price = 40m,  Unit = "kg",    ImageUrl = Img + "1546094096-0df4bcaaa337?auto=format&fit=crop&w=600&q=80" },
        new() { Id = 2, Name = "Carrot",      Price = 60m,  Unit = "kg",    ImageUrl = Img + "1598170845058-32b9d6a5da37?auto=format&fit=crop&w=600&q=80" },
        new() { Id = 3, Name = "Broccoli",    Price = 90m,  Unit = "piece", ImageUrl = Img + "1459411621453-7b03977f4bfc?auto=format&fit=crop&w=600&q=80" },
        new() { Id = 4, Name = "Spinach",     Price = 30m,  Unit = "bunch", ImageUrl = Img + "1576045057995-568f588f82fb?auto=format&fit=crop&w=600&q=80" },
        new() { Id = 5, Name = "Bell Pepper", Price = 80m,  Unit = "kg",    ImageUrl = Img + "1563565375-f3fdfdbefa83?auto=format&fit=crop&w=600&q=80" },
        new() { Id = 6, Name = "Potato",      Price = 35m,  Unit = "kg",    ImageUrl = Img + "1518977676601-b53f82aba655?auto=format&fit=crop&w=600&q=80" },
    };

    public int AddItem(int productId)
    {
        if (Products.All(p => p.Id != productId)) return 0;
        return _cart.AddOrUpdate(productId, 1, (_, qty) => qty + 1);
    }

    public int RemoveItem(int productId)
    {
        if (!_cart.TryGetValue(productId, out var qty)) return 0;
        if (qty <= 1)
        {
            _cart.TryRemove(productId, out _);
            return 0;
        }
        _cart[productId] = qty - 1;
        return qty - 1;
    }

    public int GetQuantity(int productId) => _cart.TryGetValue(productId, out var qty) ? qty : 0;

    public int GetTotalCount() => _cart.Values.Sum();

    /// <summary>Empties the cart (demo: cart starts empty on each landing page load).</summary>
    public void Clear() => _cart.Clear();

    public CartViewModel GetCart()
    {
        var lines = _cart
            .Join(Products, kv => kv.Key, p => p.Id,
                (kv, p) => new CartLineViewModel { Product = p, Quantity = kv.Value })
            .OrderBy(l => l.Product.Name)
            .ToList();
        return new CartViewModel { Lines = lines };
    }
}
