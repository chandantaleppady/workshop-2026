using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MyCartAdmin.Models;
using System.Text.Json;

namespace MyCartAdmin.Services;

public sealed class WarehouseBlobStore
{
    private readonly IConfiguration _configuration;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WarehouseBlobStore(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> UploadOrderAsync(WarehouseOrderDocument document, CancellationToken cancellationToken = default)
    {
        var container = GetContainerClient();
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobName = $"orders/{document.OrderId:D}.json";
        var blob = container.GetBlobClient(blobName);
        var payload = BinaryData.FromObjectAsJson(document, JsonOptions);
        await blob.UploadAsync(payload, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/json; charset=utf-8" },
            Metadata = new Dictionary<string, string> { ["orderid"] = document.OrderId.ToString("D") }
        }, cancellationToken);

        return blobName;
    }

    public async Task<Stream> DownloadOrderAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var blob = GetContainerClient().GetBlobClient(blobName);
        var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    private BlobContainerClient GetContainerClient()
    {
        var containerName = _configuration["BlobStorage:ContainerName"];
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException("BlobStorage:ContainerName is not configured.");
        }

        var connectionString = _configuration["BlobStorage:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new BlobContainerClient(connectionString, containerName);
        }

        var serviceUri = _configuration["BlobStorage:ServiceUri"];
        if (!Uri.TryCreate(serviceUri, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Configure BlobStorage:ServiceUri or BlobStorage:ConnectionString.");
        }

        return new BlobServiceClient(uri, new DefaultAzureCredential()).GetBlobContainerClient(containerName);
    }
}