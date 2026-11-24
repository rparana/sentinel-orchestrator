using Sentinel.Domain.Enums;

namespace Sentinel.Domain.Events;

public sealed record WorkflowStepReadyForPlanningDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid WorkflowInstanceId,
    int StepIndex) : IDomainEvent;

public sealed record WorkflowStepReadyForExecutionDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid WorkflowInstanceId,
    int StepIndex,
    int Attempt) : IDomainEvent;

public sealed record WorkflowAwaitingApprovalDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid WorkflowInstanceId,
    int StepIndex,
    string RuleName) : IDomainEvent;

public sealed record WorkflowTerminalStateReachedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid WorkflowInstanceId,
    WorkflowStatus FinalStatus) : IDomainEvent;
