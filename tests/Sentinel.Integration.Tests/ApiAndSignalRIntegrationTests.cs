using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Sentinel.Application.Workflows.Dtos;
using Sentinel.Domain.Enums;
using Sentinel.Integration.Tests.Fixtures;
using Xunit;

namespace Sentinel.Integration.Tests;

[Collection(SentinelContainersCollection.Name)]
public sealed class ApiAndSignalRIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SentinelContainersFixture _fixture;

    public ApiAndSignalRIntegrationTests(SentinelContainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StartupSeeding_And_OpenApi_WorkEndToEnd()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var workflows = await client.GetFromJsonAsync<List<WorkflowSummaryDto>>("/api/workflows", JsonOptions);
        Assert.NotNull(workflows);
        Assert.True(workflows.Count >= 3);

        var openApiResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
    }

    [Fact]
    public async Task TriggerProdScaleScenario_EmitsSignalREvents_PausesForApproval_AndCompletesAfterApprove()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var receivedUpdates = new ConcurrentBag<(Guid WorkflowId, string Status)>();
        var approvalRequested = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var hubConnection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "/hubs/workflows"),
                options => options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler())
            .Build();

        hubConnection.On<JsonElement>("WorkflowUpdated", payload =>
        {
            var id = payload.GetProperty("workflowId").GetGuid();
            var status = payload.GetProperty("status").GetString() ?? string.Empty;
            receivedUpdates.Add((id, status));
        });

        hubConnection.On<JsonElement>("ApprovalRequested", payload =>
        {
            var id = payload.GetProperty("workflowId").GetGuid();
            approvalRequested.TrySetResult(id);
        });

        await hubConnection.StartAsync();

        // Trigger prod-scale scenario
        var triggerResponse = await client.PostAsync("/api/scenarios/prod-scale/trigger", content: null);
        Assert.Equal(HttpStatusCode.Created, triggerResponse.StatusCode);

        var created = await triggerResponse.Content.ReadFromJsonAsync<WorkflowDetailDto>(JsonOptions);
        Assert.NotNull(created);

        // Wait for worker + SignalR ApprovalRequested notification
        var notifiedId = await approvalRequested.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(created.Id, notifiedId);

        // Verify workflow is AwaitingApproval via GET
        var detail = await client.GetFromJsonAsync<WorkflowDetailDto>($"/api/workflows/{created.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(WorkflowStatus.AwaitingApproval, detail.Status);

        // Approve workflow
        var approveResponse = await client.PostAsJsonAsync(
            $"/api/workflows/{created.Id}/approve",
            new { operatorName = "rafael", comment = "Approved in API integration test" });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // Poll briefly until worker completes the approved step
        WorkflowDetailDto? completed = null;
        for (var i = 0; i < 30; i++)
        {
            completed = await client.GetFromJsonAsync<WorkflowDetailDto>($"/api/workflows/{created.Id}", JsonOptions);
            if (completed?.Status == WorkflowStatus.Completed)
            {
                break;
            }
            await Task.Delay(200);
        }

        Assert.NotNull(completed);
        Assert.Equal(WorkflowStatus.Completed, completed.Status);
        Assert.Contains(receivedUpdates, u => u.WorkflowId == created.Id && u.Status == "Completed");
    }

    [Fact]
    public async Task ErrorHandling_ReturnsProblemDetailsFor404And400()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var notFound = await client.GetAsync($"/api/workflows/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("application/problem+json", notFound.Content.Headers.ContentType?.MediaType);

        var unknownScenario = await client.PostAsync("/api/scenarios/non-existent/trigger", content: null);
        Assert.Equal(HttpStatusCode.NotFound, unknownScenario.StatusCode);

        // Trigger blocked-delete and try to approve it -> 400 Bad Request (InvalidWorkflowTransitionException)
        var triggerBlocked = await client.PostAsync("/api/scenarios/blocked-delete/trigger", content: null);
        var blocked = await triggerBlocked.Content.ReadFromJsonAsync<WorkflowDetailDto>(JsonOptions);
        Assert.NotNull(blocked);

        // Wait until worker blocks it
        for (var i = 0; i < 30; i++)
        {
            var current = await client.GetFromJsonAsync<WorkflowDetailDto>($"/api/workflows/{blocked.Id}", JsonOptions);
            if (current?.Status == WorkflowStatus.BlockedByPolicy)
            {
                break;
            }
            await Task.Delay(200);
        }

        var invalidApprove = await client.PostAsJsonAsync(
            $"/api/workflows/{blocked.Id}/approve",
            new { operatorName = "rafael", comment = "Cannot approve blocked workflow" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidApprove.StatusCode);
        Assert.Equal("application/problem+json", invalidApprove.Content.Headers.ContentType?.MediaType);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:sentineldb", _fixture.PostgresConnectionString);
            builder.UseSetting("ConnectionStrings:redis", _fixture.RedisConnectionString);
            builder.UseSetting("AgentPlanner:Provider", "Mock");
            builder.UseSetting("AgentPlanner:SimulatedLatencyMs", "0");
            builder.UseSetting("Outbox:PollingIntervalMs", "50");
        });
}
