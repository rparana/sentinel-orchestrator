using Sentinel.Domain.Entities;
using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Application.Workflows.Dtos;

public sealed record WorkflowSummaryDto(
    Guid Id,
    string Name,
    string? ScenarioKey,
    WorkflowStatus Status,
    int CurrentStepIndex,
    int TotalSteps,
    uint RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record WorkflowDetailDto(
    Guid Id,
    string Name,
    string? ScenarioKey,
    WorkflowStatus Status,
    int CurrentStepIndex,
    uint RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<WorkflowStepDto> Steps,
    IReadOnlyList<WorkflowAuditEntryDto> AuditEntries);

public sealed record WorkflowStepDto(
    Guid Id,
    int Order,
    string Name,
    StepType Type,
    StepStatus Status,
    RiskTier DefaultRiskTier,
    string TargetEnvironment,
    ActionProposal? ProposedAction,
    PolicyDecision? PolicyDecision,
    string? OutputJson,
    string? ErrorMessage,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    long? DurationMs);

public sealed record WorkflowAuditEntryDto(
    Guid Id,
    DateTimeOffset Timestamp,
    string Actor,
    string EventType,
    WorkflowStatus FromStatus,
    WorkflowStatus ToStatus,
    string Details);

public static class WorkflowMappings
{
    public static WorkflowSummaryDto ToSummaryDto(this WorkflowInstance workflow) =>
        new(
            workflow.Id,
            workflow.Name,
            workflow.ScenarioKey,
            workflow.Status,
            workflow.CurrentStepIndex,
            workflow.Steps.Count,
            workflow.RowVersion,
            workflow.CreatedAt,
            workflow.UpdatedAt,
            workflow.CompletedAt);

    public static WorkflowDetailDto ToDetailDto(this WorkflowInstance workflow) =>
        new(
            workflow.Id,
            workflow.Name,
            workflow.ScenarioKey,
            workflow.Status,
            workflow.CurrentStepIndex,
            workflow.RowVersion,
            workflow.CreatedAt,
            workflow.UpdatedAt,
            workflow.CompletedAt,
            workflow.Steps
                .OrderBy(s => s.Order)
                .Select(s => new WorkflowStepDto(
                    s.Id,
                    s.Order,
                    s.Name,
                    s.Type,
                    s.Status,
                    s.DefaultRiskTier,
                    s.TargetEnvironment,
                    s.ProposedAction,
                    s.PolicyDecision,
                    s.OutputJson,
                    s.ErrorMessage,
                    s.StartedAt,
                    s.CompletedAt,
                    s.DurationMs))
                .ToList(),
            workflow.AuditEntries
                .OrderBy(a => a.Timestamp)
                .Select(a => new WorkflowAuditEntryDto(
                    a.Id,
                    a.Timestamp,
                    a.Actor,
                    a.EventType,
                    a.FromStatus,
                    a.ToStatus,
                    a.Details))
                .ToList());
}
