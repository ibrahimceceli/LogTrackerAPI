using LogTracker.Core.Pipeline;
using LogTracker.Core.Tests.Fakes;
using LogTracker.Core.Tracking;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Time.Testing;

namespace LogTracker.Core.Tests;

public sealed class RequestTrackingMiddlewareTests
{
    private static readonly DateTimeOffset StartTime = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(StartTime);
    private readonly LogQueue _queue = new(TestOptions.Create());

    [Fact]
    public async Task Records_method_route_template_path_status_duration_and_trace_id()
    {
        var context = CreateContext("GET", "/api/products/7", "?secret=token");

        await InvokeAsync(context, ctx =>
        {
            ctx.SetEndpoint(CreateEndpoint("/api/products/{id:int}"));
            ctx.Response.StatusCode = 200;
            _time.Advance(TimeSpan.FromMilliseconds(42));
            return Task.CompletedTask;
        });

        var entry = Assert.IsType<RequestLogEntry>(Assert.Single(ReadQueue()));
        Assert.Equal("GET", entry.Method);
        Assert.Equal("/api/products/{id:int}", entry.Endpoint);
        Assert.Equal("/api/products/7", entry.Path);
        Assert.Equal(200, entry.StatusCode);
        Assert.Equal(42, entry.DurationMs);
        Assert.Equal("trace-1", entry.TraceId);
        Assert.Equal(StartTime.UtcDateTime, entry.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, entry.OccurredAtUtc.Kind);
    }

    [Fact]
    public async Task Uses_unmatched_when_routing_found_no_endpoint()
    {
        var context = CreateContext("GET", "/wp-login.php");

        await InvokeAsync(context, ctx =>
        {
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        });

        var entry = Assert.IsType<RequestLogEntry>(Assert.Single(ReadQueue()));
        Assert.Equal("(unmatched)", entry.Endpoint);
        Assert.Equal(404, entry.StatusCode);
    }

    [Theory]
    [InlineData("/api/reports/summary")]
    [InlineData("/API/Reports")]
    [InlineData("/health")]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/favicon.ico")]
    public async Task Skips_excluded_paths_but_still_runs_the_request(string path)
    {
        var context = CreateContext("GET", path);
        var nextCalled = false;

        await InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
        Assert.Empty(ReadQueue());
    }

    [Fact]
    public async Task Does_not_treat_a_longer_segment_as_an_excluded_prefix()
    {
        var context = CreateContext("GET", "/healthy-products");

        await InvokeAsync(context, ctx =>
        {
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        });

        Assert.Single(ReadQueue());
    }

    [Fact]
    public async Task Skips_static_files_answered_without_an_endpoint()
    {
        var context = CreateContext("GET", "/index.html");

        await InvokeAsync(context, ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });

        Assert.Empty(ReadQueue());
    }

    [Fact]
    public async Task Records_500_and_the_error_when_an_exception_escapes()
    {
        var context = CreateContext("POST", "/api/orders");
        var exception = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync(context, ctx =>
        {
            ctx.SetEndpoint(CreateEndpoint("/api/orders"));
            throw exception;
        }));

        Assert.Same(exception, thrown);
        var entries = ReadQueue();
        var error = Assert.Single(entries.OfType<ErrorEventEntry>());
        var request = Assert.Single(entries.OfType<RequestLogEntry>());
        Assert.Equal("System.InvalidOperationException", error.ErrorType);
        Assert.Equal("/api/orders", error.Endpoint);
        Assert.Equal(500, request.StatusCode);
        Assert.Equal("/api/orders", request.Endpoint);
    }

    [Fact]
    public async Task Uses_the_endpoint_saved_by_the_exception_handler()
    {
        var context = CreateContext("GET", "/api/test/errors/timeout");

        await InvokeAsync(context, ctx =>
        {
            // This is what ExceptionHandlerMiddleware leaves behind after handling an exception.
            ctx.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature
            {
                Error = new TimeoutException(),
                Path = "/api/test/errors/timeout",
                Endpoint = CreateEndpoint("/api/test/errors/{type}"),
            });
            ctx.SetEndpoint(null);
            ctx.Response.StatusCode = 500;
            return Task.CompletedTask;
        });

        var entry = Assert.IsType<RequestLogEntry>(Assert.Single(ReadQueue()));
        Assert.Equal("/api/test/errors/{type}", entry.Endpoint);
        Assert.Equal(500, entry.StatusCode);
    }

    [Theory]
    [InlineData("api/products", "/api/products")]
    [InlineData("/api/orders/", "/api/orders")]
    [InlineData("/", "/")]
    public async Task Normalizes_route_templates(string template, string expected)
    {
        var context = CreateContext("GET", "/anything");

        await InvokeAsync(context, ctx =>
        {
            ctx.SetEndpoint(CreateEndpoint(template));
            return Task.CompletedTask;
        });

        var entry = Assert.IsType<RequestLogEntry>(Assert.Single(ReadQueue()));
        Assert.Equal(expected, entry.Endpoint);
    }

    [Fact]
    public async Task Cuts_long_paths_to_the_column_size()
    {
        var context = CreateContext("GET", "/" + new string('a', 2000));

        await InvokeAsync(context, ctx =>
        {
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        });

        var entry = Assert.IsType<RequestLogEntry>(Assert.Single(ReadQueue()));
        Assert.Equal(512, entry.Path.Length);
    }

    private Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var middleware = new RequestTrackingMiddleware(next, _queue, new ErrorRecorder(_queue, _time), TestOptions.Create(), _time);
        return middleware.InvokeAsync(context);
    }

    private List<LogEntry> ReadQueue()
    {
        var entries = new List<LogEntry>();
        while (_queue.Reader.TryRead(out var entry))
        {
            entries.Add(entry);
        }

        return entries;
    }

    private static DefaultHttpContext CreateContext(string method, string path, string query = "")
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context;
    }

    private static RouteEndpoint CreateEndpoint(string template) =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse(template), 0, EndpointMetadataCollection.Empty, template);
}
