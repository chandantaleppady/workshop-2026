using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
// using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace OrderBlobLogger;

// [StorageAccount("WarehouseStorage")]
public sealed class OrderBlobLoggerFunction
{
    private readonly ILogger<OrderBlobLoggerFunction> _logger;

    public OrderBlobLoggerFunction(ILogger<OrderBlobLoggerFunction> logger)
    {
        _logger = logger;
    }

    // Binding to BlobClient (rather than string/Stream) gives access to the blob name without downloading its content.
    [Function(nameof(OrderBlobLoggerFunction))]
    public void Run(
        [Microsoft.Azure.Functions.Worker.BlobTrigger("devstorage/test/{name}")] BlobClient blob)
    {
        _logger.LogInformation("Blob received: {BlobName}", blob.Name);
    }
}
