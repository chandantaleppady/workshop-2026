using Microsoft.AspNetCore.Mvc;
using MyCartAdmin.Models;
using MyCartAdmin.Services;

namespace MyCartAdmin.Controllers;

public sealed class OrdersController : Controller
{
    private const int PageSize = 20;
    private static readonly string[] SupportedStatuses = ["Pending", "Sending", "Sent", "Failed"];
    private readonly OrderRepository _orders;
    private readonly WarehouseDispatchService _dispatch;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        OrderRepository orders,
        WarehouseDispatchService dispatch,
        ILogger<OrdersController> logger)
    {
        _orders = orders;
        _dispatch = dispatch;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        string? status = null,
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        status = SupportedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase) ? status : null;
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
        {
            ModelState.AddModelError(string.Empty, "The start date must not be after the end date.");
            toDate = null;
        }

        var result = await _orders.GetOrdersAsync(page, PageSize, status, search, fromDate, toDate, cancellationToken);
        return View(new OrderListViewModel
        {
            Orders = result.Orders,
            Status = status,
            Search = search,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = PageSize,
            TotalCount = result.TotalCount
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orders.GetOrderAsync(id, cancellationToken);
        return order is null ? NotFound() : View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendToWarehouse(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatch.SendAsync(id, cancellationToken);
        switch (result)
        {
            case DispatchResult.Sent:
                TempData["Success"] = "The order was sent to the warehouse.";
                return RedirectToAction(nameof(Index));
            case DispatchResult.NotFound:
                return NotFound();
            case DispatchResult.NotEligible:
                TempData["Error"] = "This order is already being sent or has already been sent.";
                break;
            case DispatchResult.Failed:
                TempData["Error"] = "The warehouse upload failed. The order can be retried.";
                break;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await _dispatch.DownloadAsync(id, cancellationToken);
            return stream is null ? NotFound() : File(stream, "application/json", $"order-{id:D}.json");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Unable to download the warehouse document for order {OrderId}", id);
            return Problem("The warehouse document is currently unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}