using System.Collections.Concurrent;

namespace LogTracker.Api.Demo;

/// <summary>An order placed through the demo API.</summary>
/// <param name="Id">Order id.</param>
/// <param name="ProductId">Ordered product.</param>
/// <param name="ProductName">Product name at the time of ordering.</param>
/// <param name="Quantity">Number of items.</param>
/// <param name="UnitPrice">Price of one item.</param>
/// <param name="Total">Quantity times unit price.</param>
/// <param name="CreatedAtUtc">When the order was placed, UTC.</param>
public sealed record Order(int Id, int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Total, DateTime CreatedAtUtc);

/// <summary>
/// Keeps orders in memory. They disappear on restart, which is fine for a demo,
/// and only the most recent ones are kept so a long-running traffic simulator cannot exhaust memory.
/// </summary>
public sealed class OrderStore(TimeProvider timeProvider)
{
    private const int MaxOrders = 10_000;

    private readonly ConcurrentDictionary<int, Order> _orders = new();
    private int _lastId;

    public Order Add(Product product, int quantity)
    {
        var id = Interlocked.Increment(ref _lastId);
        var order = new Order(id, product.Id, product.Name, quantity, product.Price, product.Price * quantity,
            timeProvider.GetUtcNow().UtcDateTime);

        _orders[id] = order;
        _orders.TryRemove(id - MaxOrders, out _);
        return order;
    }

    public Order? Find(int id) => _orders.GetValueOrDefault(id);
}
