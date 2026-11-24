using Microsoft.AspNetCore.Mvc;
using Sentinel.Application.Workflows;
using Sentinel.Application.Workflows.Commands;
using Sentinel.Application.Workflows.Dtos;
using Sentinel.Application.Workflows.Queries;
using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Api.Endpoints;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var workflows = app.MapGroup("/api/workflows")
            .WithTags("Workflows");

        workflows.MapGet("/", async (
            [FromQuery] WorkflowStatus? status,
            GetWorkflowsHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetWorkflowsQuery(status), ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkflows")
        .Produces<IReadOnlyList<WorkflowSummaryDto>>(StatusCodes.Status200OK);

        workflows.MapGet("/{id:guid}", async (
            Guid id,
            GetWorkflowByIdHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetWorkflowByIdQuery(id), ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkflowById")
        .Produces<WorkflowDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        workflows.MapPost("/", async (
            CreateWorkflowRequest request,
            CreateWorkflowHandler handler,
            CancellationToken ct) =>
        {
            var command = new CreateWorkflowCommand(
                request.Name,
                request.Steps,
                request.ScenarioKey,
                request.Actor ?? "api");

            var created = await handler.HandleAsync(command, ct);
            return Results.Created($"/api/workflows/{created.Id}", created);
        })
        .WithName("CreateWorkflow")
        .Produces<WorkflowDetailDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        workflows.MapPost("/{id:guid}/approve", async (
            Guid id,
            OperatorDecisionRequest request,
            ApproveWorkflowHandler handler,
            CancellationToken ct) =>
        {
            var updated = await handler.HandleAsync(
                new ApproveWorkflowCommand(id, request.OperatorName, request.Comment),
                ct);

            return Results.Ok(updated);
        })
        .WithName("ApproveWorkflow")
        .Produces<WorkflowDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        workflows.MapPost("/{id:guid}/reject", async (
            Guid id,
            OperatorDecisionRequest request,
            RejectWorkflowHandler handler,
            CancellationToken ct) =>
        {
            var updated = await handler.HandleAsync(
                new RejectWorkflowCommand(id, request.OperatorName, request.Comment),
                ct);

            return Results.Ok(updated);
        })
        .WithName("RejectWorkflow")
        .Produces<WorkflowDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        var scenarios = app.MapGroup("/api/scenarios")
            .WithTags("Scenarios");

        scenarios.MapGet("/", () => Results.Ok(ScenarioCatalog.GetAll()))
            .WithName("GetScenarios")
            .Produces<IReadOnlyList<ScenarioDefinition>>(StatusCodes.Status200OK);

        scenarios.MapPost("/{scenarioKey}/trigger", async (
            string scenarioKey,
            TriggerScenarioHandler handler,
            CancellationToken ct) =>
        {
            var created = await handler.HandleAsync(new TriggerScenarioCommand(scenarioKey), ct);
            return Results.Created($"/api/workflows/{created.Id}", created);
        })
        .WithName("TriggerScenario")
        .Produces<WorkflowDetailDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}

public sealed record CreateWorkflowRequest(
    string Name,
    IReadOnlyList<WorkflowStepDefinition> Steps,
    string? ScenarioKey = null,
    string? Actor = null);

public sealed record OperatorDecisionRequest(
    string OperatorName,
    string Comment);
