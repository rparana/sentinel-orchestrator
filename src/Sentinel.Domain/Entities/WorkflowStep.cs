using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Domain.Entities;

public sealed class WorkflowStep
{
    public Guid Id { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public int Order { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public StepType Type { get; private set; }
    public StepStatus Status { get; private set; }
    public RiskTier DefaultRiskTier { get; private set; }
    public string TargetEnvironment { get; private set; } = string.Empty;
    public ActionProposal? ProposedAction { get; private set; }
    public PolicyDecision? PolicyDecision { get; private set; }
    public string? OutputJson { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public long? DurationMs { get; private set; }

    private WorkflowStep()
    {
    }

    internal WorkflowStep(
        Guid workflowInstanceId,
        int order,
        string name,
        StepType type,
        RiskTier defaultRiskTier,
        string targetEnvironment)
    {
        Id = Guid.NewGuid();
        WorkflowInstanceId = workflowInstanceId;
        Order = order;
        Name = name;
        Type = type;
        Status = StepStatus.Pending;
        DefaultRiskTier = defaultRiskTier;
        TargetEnvironment = targetEnvironment;
    }

    internal void MarkRunning(DateTimeOffset now)
    {
        StartedAt ??= now;
        Status = StepStatus.Running;
    }

    internal void MarkEvaluatingPolicy(ActionProposal? proposal)
    {
        ProposedAction = proposal;
        Status = StepStatus.EvaluatingPolicy;
    }

    internal void ApplyPolicyDecision(PolicyDecision decision)
    {
        PolicyDecision = decision;
        Status = decision.Verdict switch
        {
            PolicyVerdict.Allow => StepStatus.Executing,
            PolicyVerdict.RequireApproval => StepStatus.AwaitingApproval,
            PolicyVerdict.Deny => StepStatus.BlockedByPolicy,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision.Verdict, null)
        };
    }

    internal void MarkApproved()
    {
        Status = StepStatus.Executing;
    }

    internal void MarkRejected(DateTimeOffset now, string reason)
    {
        Status = StepStatus.Rejected;
        ErrorMessage = reason;
        CompleteTiming(now);
    }

    internal void MarkBlocked(DateTimeOffset now, string reason)
    {
        Status = StepStatus.BlockedByPolicy;
        ErrorMessage = reason;
        CompleteTiming(now);
    }

    internal void MarkCompleted(DateTimeOffset now, string? outputJson)
    {
        Status = StepStatus.Completed;
        OutputJson = outputJson;
        CompleteTiming(now);
    }

    internal void MarkFailed(DateTimeOffset now, string errorMessage)
    {
        Status = StepStatus.Failed;
        ErrorMessage = errorMessage;
        CompleteTiming(now);
    }

    private void CompleteTiming(DateTimeOffset now)
    {
        CompletedAt = now;
        if (StartedAt.HasValue)
        {
            DurationMs = Math.Max(0, (long)(now - StartedAt.Value).TotalMilliseconds);
        }
    }
}
