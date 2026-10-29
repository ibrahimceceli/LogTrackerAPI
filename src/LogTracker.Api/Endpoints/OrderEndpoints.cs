using LogTracker.Api.Demo;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LogTracker.Api.Endpoints;

/// <summary>Body of POST /api/orders.</summary>
/// <param name="ProductId">Id of an existing product.</param>
/// <param name="Quantity">Number of items, from 1 to 100.</param>
public sealed record CreateOrderRequest(int ProductId, int Quantity);

/// <summary>Demo order endpoints that generate traffic for LogTracker to measure.</summary>
public static class OrderEndpoints
{
    private const int MaxQuantity = 100;

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Orders");

        orders.MapPost("", CreateOrder);
        orders.MapGet("/{id:int}", GetOrder);

        return app;
    }

    /// <summary>Places an order.</summary>
    /// <remarks>Waits 30-200 ms to behave like a database write. Orders are kept in memory only.</remarks>
    /// <param name="request">Product and quantity.</param>
    /// <param name="catalog">Product source.</param>
    /// <param name="orders">Order storage.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="201">The new order; its URL is in the Location header.</response>
    /// <response code="400">The product does not exist or the quantity is outside 1-100.</response>
    public static async Task<Results<Created<Order>, ValidationProblem>> CreateOrder(
        CreateOrderRequest request, ProductCatalog catalog, OrderStore orders, CancellationToken cancellationToken)
    {
        await SimulatedLatency.WaitAsync(30, 200, cancellationToken);

        var errors = new Dictionary<string, string[]>();
        var product = catalog.Find(request.ProductId);

        if (product is null)
        {
            errors["productId"] = [$"There is no product with id {request.ProductId}."];
        }

        if (request.Quantity is < 1 or > MaxQuantity)
        {
            errors["quantity"] = [$"Must be between 1 and {MaxQuantity}."];
        }

        if (product is null || errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var order = orders.Add(product, request.Quantity);
        return TypedResults.Created($"/api/orders/{order.Id}", order);
    }

    /// <summary>Gets an order placed since the API started.</summary>
    /// <param name="id">Order id from the Location header of POST /api/orders.</param>
    /// <param name="orders">Order storage.</param>
    /// <response code="200">The order.</response>
    /// <response code="404">No order has this id.</response>
    public static Results<Ok<Order>, ProblemHttpResult> GetOrder(int id, OrderStore orders)
    {
        if (orders.Find(id) is not { } order)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order not found.",
                detail: $"There is no order with id {id}.");
        }

        return TypedResults.Ok(order);
    }
}
