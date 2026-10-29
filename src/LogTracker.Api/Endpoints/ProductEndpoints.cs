using LogTracker.Api.Demo;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LogTracker.Api.Endpoints;

/// <summary>Demo product endpoints that generate traffic for LogTracker to measure.</summary>
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/products").WithTags("Products");

        products.MapGet("", GetProducts);
        products.MapGet("/{id:int}", GetProduct);

        return app;
    }

    /// <summary>Lists all products.</summary>
    /// <remarks>Waits 10-80 ms to behave like a database call.</remarks>
    /// <response code="200">All 20 sample products.</response>
    public static async Task<Ok<IReadOnlyList<Product>>> GetProducts(ProductCatalog catalog, CancellationToken cancellationToken)
    {
        await SimulatedLatency.WaitAsync(10, 80, cancellationToken);
        return TypedResults.Ok(catalog.GetAll());
    }

    /// <summary>Gets one product.</summary>
    /// <remarks>Waits 10-80 ms to behave like a database call.</remarks>
    /// <param name="id">Product id, from 1 to 20.</param>
    /// <param name="catalog">Product source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The product.</response>
    /// <response code="404">No product has this id.</response>
    public static async Task<Results<Ok<Product>, ProblemHttpResult>> GetProduct(
        int id, ProductCatalog catalog, CancellationToken cancellationToken)
    {
        await SimulatedLatency.WaitAsync(10, 80, cancellationToken);

        if (catalog.Find(id) is not { } product)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product not found.",
                detail: $"There is no product with id {id}.");
        }

        return TypedResults.Ok(product);
    }
}
