# Order Blob Logger Azure Function Specification

## Purpose

Build an Azure Function that is triggered when a blob is created or updated in the warehouse order container and logs the name of that blob. Logging the blob name is the application's only responsibility.

## Scope and Assumptions

- The application is a separate Azure Functions project in the existing solution (`workshop-2026.sln`), placed under `Projects/`.
- It uses the .NET isolated worker model, targeting the same .NET version as the other projects (`net10.0`).
- It monitors the same storage account, container, and folder that `MyCartAdmin` uploads order JSON documents to (`BlobStorage:ContainerName` and `BlobStorage:FolderName`).
- The function only reads the blob name from the trigger metadata. It does not open, download, parse, or validate the blob content.
- Local development may use Azurite or a development storage account.

## Functional Requirements

1. The function is triggered by an Azure Blob Storage trigger on the configured container and folder path.
2. When triggered, the function logs the blob name at `Information` level, for example: `Blob received: orders/3f2b8c1e-....json`.
3. The function does nothing else. It must not:
   - read or deserialize the blob content,
   - modify, move, copy, or delete the blob,
   - write to SQL Server or any other data store,
   - call other services or produce output bindings.

## Trigger Binding

- Trigger type: `BlobTrigger`.
- Path pattern: `{ContainerName}/{FolderName}/{name}`, supplied through app settings rather than hard-coded, for example `%BlobContainerName%/%BlobFolderName%/{name}`.
- The trigger parameter should be bound as a `BlobClient` (deferred binding) and the blob name logged from `BlobClient.Name`. Do not bind the trigger as a `Stream`, `string`, or `byte[]`; in the isolated worker those types cause the runtime to download the blob content.
- Storage connection: a named connection setting (for example `WarehouseStorage`).

## Configuration

Settings are supplied through `local.settings.json` for local development and Function App application settings when deployed. Do not commit secrets. Example shape:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "WarehouseStorage": "<Azure Storage connection string>",
    "BlobContainerName": "devstorage",
    "BlobFolderName": "orders"
  }
}
```

- `local.settings.json` must be excluded from source control.
- `BlobContainerName` and `BlobFolderName` must match the values `MyCartAdmin` uses for uploads.

## Logging

- Use the injected `ILogger` with structured logging, for example `logger.LogInformation("Blob received: {BlobName}", blob.Name);`.
- Log only the blob name. Do not log blob content or connection strings.
- Logs are available in the Functions host console locally and in Application Insights when deployed.

## Error Handling

- The function has no business logic that can fail. Unhandled host or binding errors are left to the Functions runtime's default retry and poison-blob handling.

## Non-Functional Requirements

- Minimal dependencies: only the Azure Functions worker SDK and the Storage Blobs extension. Logs reach Application Insights through the Functions host when `APPLICATIONINSIGHTS_CONNECTION_STRING` is configured.
- No changes to `MyCartAdmin` or `MyShoppingCart` are required.

## Acceptance Criteria

- The project builds as part of `workshop-2026.sln`.
- Running locally and uploading a blob to `{BlobContainerName}/{BlobFolderName}/` produces exactly one log entry containing that blob name.
- Sending an order to the warehouse from `MyCartAdmin` produces a log entry containing the uploaded blob name, for example `orders/{OrderId}.json`.
- Blobs uploaded outside the configured folder do not trigger the function.
- The function never reads, changes, or deletes the blob.

## Out of Scope

- Reading, parsing, or validating the order JSON.
- Updating order dispatch status in SQL Server.
- Notifications, alerts, or forwarding the blob to other systems.
- Event Grid–based triggering; the standard blob trigger is sufficient for this scope.
