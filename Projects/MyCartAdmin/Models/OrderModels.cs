using System.ComponentModel.DataAnnotations;

namespace MyCartAdmin.Models;

public sealed class OrderSummary
{
    public Guid OrderId { get; init; }
    public DateTime OrderDateUtc { get; init; }
    public int ItemCount { get; init; }
    public decimal OrderTotal { get; init; }
    public string DispatchStatus { get; init; } = "Pending";
    public DateTime? SentToWarehouseAtUtc { get; init; }
    public string? WarehouseBlobName { get; init; }
}

public sealed class OrderLine
{
    public long OrderDetailId { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public sealed class OrderDetail
{
    public Guid OrderId { get; init; }
    public DateTime OrderDateUtc { get; init; }
    public string DispatchStatus { get; init; } = "Pending";
    public DateTime? SentToWarehouseAtUtc { get; init; }
    public string? WarehouseBlobName { get; init; }
    public string? LastDispatchError { get; init; }
    public List<OrderLine> Items { get; init; } = [];
    public int ItemCount => Items.Sum(item => item.Quantity);
    public decimal OrderTotal => Items.Sum(item => item.LineTotal);
}

public sealed class OrderListViewModel
{
    public IReadOnlyList<OrderSummary> Orders { get; init; } = [];
    public string? Status { get; init; }
    public string? Search { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int PageCount => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class LoginViewModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed record WarehouseOrderDocument(
    Guid OrderId,
    DateTime OrderDateUtc,
    DateTime SentToWarehouseAtUtc,
    IReadOnlyList<WarehouseOrderLine> Items,
    int ItemCount,
    decimal OrderTotal);

public sealed record WarehouseOrderLine(
    int ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);