using System.Net;
using System.Net.Http.Json;

namespace LogTracker.Api.Tests;

public sealed class DemoEndpointTests : IAsyncLifetime
{
    private readonly LogTrackerApiFactory _factory = new();
    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Lists_twenty_products()
    {
        var products = await (await _client.GetAsync("/api/products", Ct)).ReadJsonAsync(HttpStatusCode.OK);

        Assert.Equal(20, products.GetArrayLength());
        JsonAssert.HasProperties(products[0], "id", "name", "category", "price");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public async Task Returns_known_products(int id)
    {
        var product = await (await _client.GetAsync($"/api/products/{id}", Ct)).ReadJsonAsync(HttpStatusCode.OK);

        Assert.Equal(id, product.GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task Returns_404_problem_for_unknown_products(int id)
    {
        await (await _client.GetAsync($"/api/products/{id}", Ct)).ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creates_an_order_that_can_be_read_back()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new { productId = 3, quantity = 2 }, Ct);

        var order = await response.ReadJsonAsync(HttpStatusCode.Created);
        Assert.Equal($"/api/orders/{order.GetProperty("id").GetInt32()}", response.Headers.Location?.ToString());
        Assert.Equal(2, order.GetProperty("quantity").GetInt32());

        var readBack = await (await _client.GetAsync(response.Headers.Location, Ct)).ReadJsonAsync(HttpStatusCode.OK);
        Assert.Equal(order.GetProperty("id").GetInt32(), readBack.GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData(3, 0, "quantity")]
    [InlineData(3, 101, "quantity")]
    [InlineData(99, 1, "productId")]
    public async Task Rejects_invalid_orders_with_validation_problem(int productId, int quantity, string invalidField)
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new { productId, quantity }, Ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty(invalidField, out _));
    }

    [Fact]
    public async Task Rejects_malformed_json_with_400_problem_and_records_no_error()
    {
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/orders", content, Ct);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Empty(_factory.Store.Errors);
    }

    [Fact]
    public async Task Returns_404_problem_for_unknown_orders()
    {
        await (await _client.GetAsync("/api/orders/123456", Ct)).ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_routes_get_a_problem_details_body()
    {
        await (await _client.GetAsync("/api/does-not-exist", Ct)).ReadProblemAsync(HttpStatusCode.NotFound);
    }
}
