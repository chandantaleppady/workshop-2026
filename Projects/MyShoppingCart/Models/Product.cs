namespace MyShoppingCart.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public class CartItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class CartLineViewModel
{
    public Product Product { get; set; } = new();
    public int Quantity { get; set; }
    public decimal LineTotal => Product.Price * Quantity;
}

public class CartViewModel
{
    public List<CartLineViewModel> Lines { get; set; } = new();
    public int TotalCount => Lines.Sum(l => l.Quantity);
    public decimal GrandTotal => Lines.Sum(l => l.LineTotal);
}
