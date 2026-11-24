using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Sentinel.Api.Endpoints;
using Sentinel.Api.Hubs;
using Sentinel.Api.Middleware;
using Sentinel.Api.Seeding;
using Sentinel.Application.Abstractions;
using Sentinel.Application.Workflows.Commands;
using Sentinel.Application.Workflows.Queries;
using Sentinel.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IWorkflowNotificationPublisher, SignalRWorkflowNotificationPublisher>();
builder.Services.AddSentinelInfrastructure(builder.Configuration);

builder.Services.AddScoped<CreateWorkflowHandler>();
builder.Services.AddScoped<TriggerScenarioHandler>();
builder.Services.AddScoped<ApproveWorkflowHandler>();
builder.Services.AddScoped<RejectWorkflowHandler>();
builder.Services.AddScoped<GetWorkflowsHandler>();
builder.Services.AddScoped<GetWorkflowByIdHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapDefaultEndpoints();
app.MapWorkflowEndpoints();
app.MapHub<WorkflowHub>("/hubs/workflows");

app.MapGet("/api/status", () => Results.Ok(new
{
    service = "Sentinel.Api",
    status = "Healthy",
    timestamp = DateTimeOffset.UtcNow
}))
.WithTags("Health");

await DatabaseSeeder.MigrateAndSeedAsync(app.Services);

app.Run();

public partial class Program;
