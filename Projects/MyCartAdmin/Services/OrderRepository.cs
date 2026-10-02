using System.Data;
using Microsoft.Data.SqlClient;
using MyCartAdmin.Models;

namespace MyCartAdmin.Services;

public sealed class OrderRepository
{
    private readonly string _connectionString;

    public OrderRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OrdersDatabase")
            ?? throw new InvalidOperationException("Connection string 'OrdersDatabase' is not configured.");
    }

    public async Task<(IReadOnlyList<OrderSummary> Orders, long TotalCount)> GetOrdersAsync(
        int page,
        int pageSize,
        string? status,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        const string fromSql = """
            FROM dbo.Orders o
            LEFT JOIN dbo.OrderDispatchState d ON d.OrderId = o.OrderId
            WHERE (@Status IS NULL OR COALESCE(d.DispatchStatus, 'Pending') = @Status)
              AND (@OrderId IS NULL OR o.OrderId = @OrderId)
              AND (@FromDate IS NULL OR o.OrderDate >= @FromDate)
              AND (@ToDate IS NULL OR o.OrderDate < DATEADD(day, 1, @ToDate))
            """;

        Guid? orderId = Guid.TryParse(search, out var parsedOrderId) ? parsedOrderId : null;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var countCommand = new SqlCommand($"SELECT COUNT_BIG(DISTINCT o.OrderId) {fromSql}", connection);
        AddFilters(countCommand, status, orderId, fromDate, toDate);
        var totalCount = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);

        const string listSql = """
            SELECT o.OrderId,
                   MIN(o.OrderDate) AS OrderDateUtc,
                   SUM(o.Quantity) AS ItemCount,
                   SUM(o.UnitPrice * o.Quantity) AS OrderTotal,
                   COALESCE(MAX(d.DispatchStatus), 'Pending') AS DispatchStatus,
                   MAX(d.SentToWarehouseAtUtc) AS SentToWarehouseAtUtc,
                   MAX(d.WarehouseBlobName) AS WarehouseBlobName
            """;
        var sql = $"""
            {listSql}
            {fromSql}
            GROUP BY o.OrderId
            ORDER BY MIN(o.OrderDate) DESC, o.OrderId DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var listCommand = new SqlCommand(sql, connection);
        AddFilters(listCommand, status, orderId, fromDate, toDate);
        listCommand.Parameters.Add("@Offset", SqlDbType.Int).Value = (page - 1) * pageSize;
        listCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

        var orders = new List<OrderSummary>();
        await using var reader = await listCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            orders.Add(new OrderSummary
            {
                OrderId = reader.GetGuid(0),
                OrderDateUtc = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc),
                ItemCount = reader.GetInt32(2),
                OrderTotal = reader.GetDecimal(3),
                DispatchStatus = reader.GetString(4),
                SentToWarehouseAtUtc = reader.IsDBNull(5) ? null : DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
                WarehouseBlobName = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return (orders, totalCount);
    }

    public async Task<OrderDetail?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT o.OrderDetailId, o.OrderDate, o.ProductId, o.ProductName, o.UnitPrice, o.Quantity,
                   COALESCE(d.DispatchStatus, 'Pending') AS DispatchStatus,
                   d.SentToWarehouseAtUtc, d.WarehouseBlobName, d.LastDispatchError
            FROM dbo.Orders o
            LEFT JOIN dbo.OrderDispatchState d ON d.OrderId = o.OrderId
            WHERE o.OrderId = @OrderId
            ORDER BY o.OrderDetailId;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;

        OrderDetail? detail = null;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            detail ??= new OrderDetail
            {
                OrderId = orderId,
                OrderDateUtc = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc),
                DispatchStatus = reader.GetString(6),
                SentToWarehouseAtUtc = reader.IsDBNull(7) ? null : DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc),
                WarehouseBlobName = reader.IsDBNull(8) ? null : reader.GetString(8),
                LastDispatchError = reader.IsDBNull(9) ? null : reader.GetString(9)
            };

            detail.Items.Add(new OrderLine
            {
                OrderDetailId = reader.GetInt64(0),
                ProductId = reader.GetInt32(2),
                ProductName = reader.GetString(3),
                UnitPrice = reader.GetDecimal(4),
                Quantity = reader.GetInt32(5)
            });
        }

        return detail;
    }

    public async Task<bool> TryStartDispatchAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        const string existsSql = "SELECT DispatchStatus FROM dbo.OrderDispatchState WITH (UPDLOCK, HOLDLOCK) WHERE OrderId = @OrderId;";
        await using var checkCommand = new SqlCommand(existsSql, connection, transaction);
        checkCommand.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
        var currentStatus = await checkCommand.ExecuteScalarAsync(cancellationToken) as string;

        if (currentStatus is null)
        {
            const string hasOrderSql = "SELECT COUNT_BIG(1) FROM dbo.Orders WHERE OrderId = @OrderId;";
            await using var orderCommand = new SqlCommand(hasOrderSql, connection, transaction);
            orderCommand.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
            var hasOrder = (long)(await orderCommand.ExecuteScalarAsync(cancellationToken) ?? 0L) > 0;
            if (!hasOrder)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            const string insertSql = "INSERT INTO dbo.OrderDispatchState (OrderId, DispatchStatus, AttemptStartedAtUtc) VALUES (@OrderId, 'Sending', SYSUTCDATETIME());";
            await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else if (currentStatus is "Pending" or "Failed")
        {
            const string updateSql = """
                UPDATE dbo.OrderDispatchState
                SET DispatchStatus = 'Sending', AttemptStartedAtUtc = SYSUTCDATETIME(),
                    LastDispatchError = NULL, SentToWarehouseAtUtc = NULL
                WHERE OrderId = @OrderId;
                """;
            await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
            updateCommand.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task MarkSentAsync(Guid orderId, string blobName, CancellationToken cancellationToken = default) =>
        UpdateDispatchAsync(orderId, "Sent", blobName, null, cancellationToken);

    public Task MarkFailedAsync(Guid orderId, string safeError, CancellationToken cancellationToken = default) =>
        UpdateDispatchAsync(orderId, "Failed", null, safeError, cancellationToken);

    private async Task UpdateDispatchAsync(Guid orderId, string status, string? blobName, string? error, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.OrderDispatchState
            SET DispatchStatus = @Status,
                WarehouseBlobName = COALESCE(@BlobName, WarehouseBlobName),
                SentToWarehouseAtUtc = CASE WHEN @Status = 'Sent' THEN SYSUTCDATETIME() ELSE NULL END,
                LastDispatchError = @Error
            WHERE OrderId = @OrderId AND DispatchStatus = 'Sending';
            """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = status;
        command.Parameters.Add("@BlobName", SqlDbType.NVarChar, 500).Value = (object?)blobName ?? DBNull.Value;
        command.Parameters.Add("@Error", SqlDbType.NVarChar, 1000).Value = (object?)error ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddFilters(SqlCommand command, string? status, Guid? orderId, DateTime? fromDate, DateTime? toDate)
    {
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = (object?)status ?? DBNull.Value;
        command.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = (object?)orderId ?? DBNull.Value;
        command.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value = (object?)fromDate ?? DBNull.Value;
        command.Parameters.Add("@ToDate", SqlDbType.DateTime2).Value = (object?)toDate ?? DBNull.Value;
    }
}