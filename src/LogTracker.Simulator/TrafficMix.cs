namespace LogTracker.Simulator;

/// <summary>One HTTP call the simulator is about to make.</summary>
/// <param name="Path">Path relative to the API base URL, for example "/api/products/3".</param>
/// <param name="Body">Optional object that is sent as a JSON body.</param>
internal sealed record PlannedRequest(HttpMethod Method, string Path, object? Body = null);

/// <summary>JSON body for POST /api/orders.</summary>
internal sealed record CreateOrderRequest(int ProductId, int Quantity);

/// <summary>
/// Decides which demo endpoint to call next. The mix mostly produces successful calls,
/// with some realistic client mistakes (unknown ids, invalid orders), a few slow calls
/// and a configurable share of server errors - enough variety to fill every dashboard chart.
/// </summary>
internal sealed class TrafficMix(int errorPercent)
{
    // The demo API serves products with ids 1-20.
    private const int ProductCount = 20;
    private const int MaxRememberedOrders = 100;

    private static readonly string[] ErrorTypes =
        ["invalid-operation", "null-reference", "divide-by-zero", "timeout", "argument", "key-not-found"];

    // Order ids returned by successful POST /api/orders calls, so that GET /api/orders/{id}
    // can ask for orders that really exist. Several virtual users share this list.
    private readonly List<int> _createdOrderIds = [];
    private readonly Lock _orderIdsLock = new();

    public PlannedRequest Next()
    {
        // Decide on errors first so that --error-percent means exactly what it says.
        if (Random.Shared.Next(100) < errorPercent)
        {
            return TriggerServerError();
        }

        return Random.Shared.Next(100) switch
        {
            < 30 => new PlannedRequest(HttpMethod.Get, "/api/products"),
            < 55 => GetProduct(),
            < 75 => CreateOrder(),
            < 95 => GetOrder(),
            _ => new PlannedRequest(HttpMethod.Get, $"/api/test/slow?ms={Random.Shared.Next(500, 3001)}"),
        };
    }

    /// <summary>Learns from responses, e.g. remembers the id of every order that was created.</summary>
    public void Observe(PlannedRequest request, HttpResponseMessage response)
    {
        bool isCreatedOrder = request.Body is CreateOrderRequest && response.StatusCode == System.Net.HttpStatusCode.Created;
        if (!isCreatedOrder || !TryReadOrderId(response.Headers.Location, out int orderId))
        {
            return;
        }

        lock (_orderIdsLock)
        {
            _createdOrderIds.Add(orderId);
            if (_createdOrderIds.Count > MaxRememberedOrders)
            {
                _createdOrderIds.RemoveAt(0);
            }
        }
    }

    private static PlannedRequest GetProduct()
    {
        // About 15% of the lookups use an id that does not exist and should return 404.
        int id = Random.Shared.Next(100) < 15
            ? Random.Shared.Next(ProductCount + 1, 500)
            : Random.Shared.Next(1, ProductCount + 1);
        return new PlannedRequest(HttpMethod.Get, $"/api/products/{id}");
    }

    private static PlannedRequest CreateOrder()
    {
        var order = new CreateOrderRequest(Random.Shared.Next(1, ProductCount + 1), Random.Shared.Next(1, 6));

        // About 20% of the orders are invalid and should return 400:
        // half of them for an unknown product, half with a quantity outside 1-100.
        int roll = Random.Shared.Next(100);
        if (roll < 10)
        {
            order = order with { ProductId = Random.Shared.Next(ProductCount + 1, 1000) };
        }
        else if (roll < 20)
        {
            order = order with { Quantity = Random.Shared.Next(2) == 0 ? 0 : Random.Shared.Next(101, 1000) };
        }

        return new PlannedRequest(HttpMethod.Post, "/api/orders", order);
    }

    private PlannedRequest GetOrder()
    {
        int? knownId = null;
        lock (_orderIdsLock)
        {
            if (_createdOrderIds.Count > 0)
            {
                knownId = _createdOrderIds[Random.Shared.Next(_createdOrderIds.Count)];
            }
        }

        // Mostly ask for an order that was created, sometimes for one that does not exist (404).
        int id = knownId is int existing && Random.Shared.Next(100) < 80
            ? existing
            : Random.Shared.Next(100_000, 1_000_000);
        return new PlannedRequest(HttpMethod.Get, $"/api/orders/{id}");
    }

    private static PlannedRequest TriggerServerError()
    {
        string type = ErrorTypes[Random.Shared.Next(ErrorTypes.Length)];
        return new PlannedRequest(HttpMethod.Get, $"/api/test/errors/{type}");
    }

    private static bool TryReadOrderId(Uri? location, out int orderId)
    {
        orderId = 0;
        if (location is null)
        {
            return false;
        }

        // Location may be absolute ("http://api:8080/api/orders/7") or relative ("/api/orders/7").
        string path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        string lastSegment = path.TrimEnd('/').Split('/')[^1];
        return int.TryParse(lastSegment, out orderId);
    }
}
