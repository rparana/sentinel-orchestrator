var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/api/status", () => Results.Ok(new
{
    service = "Sentinel.Api",
    status = "Healthy",
    timestamp = DateTimeOffset.UtcNow
}));

app.Run();
