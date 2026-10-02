using Microsoft.Data.SqlClient;
using MyShoppingCart.Models;

namespace MyShoppingCart.Services;

public class OrderService
{
    private readonly string _connectionString;

    public OrderService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OrdersDatabase")
            ?? throw new InvalidOperationException("Connection string 'OrdersDatabase' is not configured.");
    }

    public async Task<Guid> SaveOrderAsync(CartViewModel cart)
    {
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