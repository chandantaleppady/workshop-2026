using Microsoft.Data.SqlClient;
using MyShoppingCart.Models;

namespace MyShoppingCart.Services;

public class OrderService
{
    private readonly string? _connectionString;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IConfiguration configuration, ILogger<OrderService> logger)
    {
        _connectionString = configuration.GetConnectionString("OrdersDatabase");
        _logger = logger;
    }

    public async Task<Guid> SaveOrderAsync(CartViewModel cart)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogError("Connection string '{ConnectionStringName}' is not configured.", "OrdersDatabase");
            throw new InvalidOperationException("Connection string 'OrdersDatabase' is not configured.");
        }

        var orderId = Guid.NewGuid();
        const string sql = """
            INSERT INTO dbo.Orders (OrderId, ProductId, ProductName, UnitPrice, Quantity)
            VALUES (@OrderId, @ProductId, @ProductName, @UnitPrice, @Quantity);
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        foreach (var line in cart.Lines)
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@OrderId", orderId);
            command.Parameters.AddWithValue("@ProductId", line.Product.Id);
            command.Parameters.AddWithValue("@ProductName", line.Product.Name);
            command.Parameters.AddWithValue("@UnitPrice", line.Product.Price);
            command.Parameters.AddWithValue("@Quantity", line.Quantity);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return orderId;
    }
}