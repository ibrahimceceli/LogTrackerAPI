using LogTracker.Api.Demo;
using LogTracker.Api.Endpoints;
using LogTracker.Core;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogTracker(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ProductCatalog>();
builder.Services.AddSingleton<OrderStore>();

var app = builder.Build();

// The dashboard in wwwroot is served before tracking starts, so its files are never logged.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseLogTracker();

// Gives empty error responses, such as 404 for an unknown route, a ProblemDetails body.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");
app.MapLogTrackerReports();
app.MapProductEndpoints();
app.MapOrderEndpoints();

// Read after Build so environment variables and test hosts can switch it.
if (app.Configuration.GetValue<bool>("LogTracker:EnableTestEndpoints"))
{
    app.MapTestEndpoints();
}

app.Run();
