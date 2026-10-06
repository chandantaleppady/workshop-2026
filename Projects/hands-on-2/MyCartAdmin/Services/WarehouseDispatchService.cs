using System.Text.Json;
using MyCartAdmin.Models;

namespace MyCartAdmin.Services;

public sealed class WarehouseDispatchService
{
    private readonly OrderRepository _orders;
    private readonly WarehouseBlobStore _blobStore;
    private readonly ILogger<WarehouseDispatchService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WarehouseDispatchService(
        OrderRepository orders,
        WarehouseBlobStore blobStore,
        ILogger<WarehouseDispatchService> logger)
    {
        _orders = orders;
        _blobStore = blobStore;
        _logger = logger;
    }

    public async Task<DispatchResult> SendAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var detail = await _orders.GetOrderAsync(orderId, cancellationToken);
        if (detail is null) return DispatchResult.NotFound;

        if (!await _orders.TryStartDispatchAsync(orderId, cancellationToken))
        {
            return DispatchResult.NotEligible;
        }

        try
        {
            var sentAt = DateTime.UtcNow;
            var document = new WarehouseOrderDocument(
                detail.OrderId,
                detail.OrderDateUtc,
                sentAt,
                detail.Items.Select(item => new WarehouseOrderLine(
                    item.ProductId,
                    item.ProductName,
                    item.UnitPrice,
                    item.Quantity,
                    item.LineTotal)).ToArray(),
                detail.ItemCount,
                detail.OrderTotal);
            var blobName = await _blobStore.UploadOrderAsync(document, cancellationToken);
            await _orders.MarkSentAsync(orderId, blobName, cancellationToken);
            return DispatchResult.Sent;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Warehouse dispatch failed for order {OrderId}", orderId);
            try
            {
                await _orders.MarkFailedAsync(orderId, "Warehouse upload failed. Retry the dispatch.", cancellationToken);
            }
            catch (Exception updateException)
            {
                _logger.LogError(updateException, "Failed to record dispatch failure for order {OrderId}", orderId);
            }

            return DispatchResult.Failed;
        }
    }

    public async Task<Stream?> DownloadAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var detail = await _orders.GetOrderAsync(orderId, cancellationToken);
        if (detail?.DispatchStatus != "Sent" || string.IsNullOrWhiteSpace(detail.WarehouseBlobName)) return null;
        return await _blobStore.DownloadOrderAsync(detail.WarehouseBlobName, cancellationToken);
    }
}

public enum DispatchResult
{
    Sent,
    NotFound,
    NotEligible,
    Failed
}