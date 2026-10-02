# Order Management MVC Application Specification

## Purpose

Build an administrator-facing ASP.NET Core MVC application for reviewing customer orders and sending an order's details to a warehouse. Sending an order means creating a JSON document and uploading it to Azure Blob Storage; this scope does not include calling a warehouse API.

## Scope and Assumptions

- The application is a separate MVC application in the existing solution, using the existing SQL Server orders database.
- Existing order line rows are grouped by `OrderId`. The current `dbo.Orders` table does not contain order-level fulfillment status or blob metadata.
- The database schema will be migrated to represent an order header and its lines separately, while preserving existing orders.
- The blob target is Azure Blob Storage. Local development may use Azurite.
- Admin routes require authentication and an administrator role. The identity provider and deployment-specific authentication configuration are deployment decisions.
- “Send to warehouse” means upload the JSON order document to a configured blob container and record that result in SQL Server.

## User Stories

1. As an administrator, I can see the orders currently stored in the database so I can decide which orders need fulfillment.
2. As an administrator, I can inspect an order's items, quantities, prices, and total before dispatching it.
3. As an administrator, I can send an eligible order to the warehouse and see whether the operation succeeded.
4. As an administrator, I can retry a failed dispatch without creating a second logical warehouse document.

## MVC Experience

### Order List

- The default admin page lists orders, newest first.
- Each row shows order ID, order date/time in the configured display time zone, item count, order total, and fulfillment status.
- Provide pagination and filters for status, order ID, and date range.
- Each order links to a details view.
- Loading the list must not call Blob Storage; SQL Server is the source of truth for order and dispatch status.

### Order Details

- Show order ID, order date, fulfillment status, dispatch timestamp when available, and every line item with product ID, name, unit price, quantity, and line total.
- Show the order total and a link to the stored blob when the order has been sent and the signed-in admin is authorized to access it.
- Display a confirmation step before dispatch. Disable dispatch for orders already sent or currently being sent.
- Show clear success and failure messages. A failed dispatch remains available for retry.

### Authorization

- All order list, detail, dispatch, and blob-link actions require an authenticated administrator.
- Dispatch is an anti-forgery-protected POST action; it must not be triggered by a GET request.
- Do not expose storage credentials, connection strings, or unrestricted public blob URLs in rendered HTML or logs.

## Data and Persistence

### Target Logical Model

Use an order-level record and associated line records. Suggested SQL Server tables:

- `OrderHeaders`: `OrderId` primary key, `OrderDate`, `DispatchStatus`, `SentToWarehouseAtUtc` nullable, `WarehouseBlobName` nullable, `LastDispatchError` nullable, and a `rowversion` concurrency token.
- `OrderItems`: `OrderDetailId` primary key, `OrderId` foreign key, `ProductId`, `ProductName`, `UnitPrice`, and `Quantity`. `LineTotal` may be computed as `UnitPrice * Quantity`.

Suggested dispatch statuses are `Pending`, `Sending`, `Sent`, and `Failed`. Only `Pending` and `Failed` orders can be dispatched. Status updates must be concurrency-safe so simultaneous admin requests cannot create competing dispatches.

### Migration

The existing `dbo.Orders` table contains one detail row per product and repeats `OrderId` and `OrderDate`. Provide a migration that:

1. Groups existing rows by `OrderId` to create one `OrderHeaders` row per order.
2. Copies each existing row to `OrderItems` without changing its product, price, or quantity.
3. Sets migrated orders to `Pending` because no warehouse dispatch history exists.
4. Verifies row counts and order totals before retiring or renaming the old table.

Migration must be safe to run through the project's normal deployment migration process and must not silently discard existing order data.

## Warehouse Dispatch

### Workflow

1. The admin submits the dispatch POST action for an eligible `OrderId`.
2. The application reloads the order and its lines from SQL Server; client-submitted prices, quantities, totals, or JSON are never trusted.
3. The application atomically changes the status from `Pending` or `Failed` to `Sending`, using a conditional update and the concurrency token.
4. It builds the JSON payload from the database snapshot and uploads it to the configured private blob container.
5. After a successful upload, it records status `Sent`, the blob name, and `SentToWarehouseAtUtc` in SQL Server.
6. If upload fails, it records `Failed` with a sanitized error for display and logging. The order remains eligible for retry.

SQL Server and Blob Storage do not share a transaction. Use a deterministic blob name such as `orders/{OrderId}.json` and make retries idempotent. If the blob upload succeeds but the final SQL update fails, retrying the same order must safely replace or verify the same logical document rather than create a duplicate. Provide a recovery path for stale `Sending` rows, such as an admin retry action after a timeout or a scheduled reconciliation process.

### JSON Contract

Write UTF-8 JSON using camelCase property names. The document contains:

- `orderId`: order GUID
- `orderDateUtc`: ISO 8601 UTC timestamp
- `sentToWarehouseAtUtc`: ISO 8601 UTC timestamp
- `items`: array of `productId`, `productName`, `unitPrice`, `quantity`, and `lineTotal`
- `itemCount`: sum of item quantities
- `orderTotal`: sum of line totals

Monetary values are JSON numbers serialized with decimal precision; calculations use the database snapshot. Do not include customer or payment details unless a later requirement explicitly adds them.

### Blob Storage

- Store documents in a private container configured for the application.
- Save the container name and service URI in configuration; use managed identity in deployed environments where available.
- Do not commit secrets. Use user secrets or environment-specific secret storage for local connection strings or storage credentials.
- Set blob content type to `application/json` and attach `OrderId` as blob metadata.
- The UI accesses the document through an authorized application action that streams it or creates a short-lived, read-only SAS URL. Do not make the container public.

## Configuration

Database and Blob Storage settings are supplied through ASP.NET Core configuration, with environment variables/user secrets taking precedence over committed defaults. Example shape:

```json
{
  "ConnectionStrings": {
    "OrdersDatabase": "<SQL Server connection string>"
  },
  "BlobStorage": {
    "ServiceUri": "<Azure Blob service URI>",
    "ContainerName": "warehouse-orders"
  }
}
```

For local development, configure a SQL Server instance/database and Azurite or a development storage account. Validate required settings at startup and report a clear configuration error without logging secret values.

## Error Handling and Auditability

- Distinguish unavailable database, unavailable blob storage, invalid order state, and concurrency conflict in logs and admin feedback.
- Show sanitized messages to administrators; keep exception details and credentials out of the UI.
- Record dispatch attempt time, outcome, and a safe failure summary. Do not log the full payload if it could later contain sensitive information.
- Repeated dispatch requests for an order already marked `Sent` return a non-destructive “already sent” result and do not produce another logical blob.

## Non-Functional Requirements

- Use asynchronous SQL and Blob Storage APIs.
- Use parameterized SQL and least-privilege database/storage identities.
- Paginate order lists in the database rather than loading the entire order history.
- Keep blob containers private and protect all admin endpoints against CSRF.
- Make dispatch operations observable with structured logs keyed by `OrderId` and attempt ID.
- Provide automated coverage for authorization, grouping, totals, valid and invalid status transitions, successful dispatch, blob failure, retry/idempotency, and concurrent dispatch attempts.

## Acceptance Criteria

1. An authorized admin can view a paginated list of database orders with correct item counts, totals, and statuses.
2. An unauthorized user cannot access the list, details, dispatch action, or stored documents.
3. The detail page shows values calculated from the database, not values supplied by the browser.
4. Dispatching an eligible order writes a valid JSON document to the configured private blob container and records the blob name, timestamp, and `Sent` status in SQL Server.
5. The JSON document contains every order line and totals matching the database snapshot.
6. A failed upload leaves the order retryable and visibly marked `Failed`; a retry uses the same logical blob name.
7. Repeated or simultaneous dispatch requests cannot create duplicate logical warehouse documents or overwrite a successful status with a failure.
8. Existing orders remain available after migration, with their original line details and totals preserved.
9. Database and Blob Storage configuration can be overridden per environment without source changes or committed secrets.
10. Automated tests cover the above behavior using a test database and a Blob Storage test double or Azurite.

## Out of Scope

- Warehouse APIs, shipment tracking, carrier integrations, inventory reservation, payments, customer-facing order history, and editing or deleting placed order lines.
- User/role provisioning and organization-specific identity provider setup.
- Public blob access or indefinite SAS links.
