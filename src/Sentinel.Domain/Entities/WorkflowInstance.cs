using Sentinel.Domain.Enums;
using Sentinel.Domain.Exceptions;
using Sentinel.Domain.Policies;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Domain.Entities;

public sealed class WorkflowInstance
{
    private readonly List<WorkflowStep> _steps = [];
    private readonly List<WorkflowAuditEntry> _auditEntries = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? ScenarioKey { get; private set; }
    public WorkflowStatus Status { get; private set; }
    public int CurrentStepIndex { get; private set; }
    public uint RowVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyList<WorkflowStep> Steps => _steps.AsReadOnly();
    public IReadOnlyList<WorkflowAuditEntry> AuditEntries => _auditEntries.AsReadOnly();

    public WorkflowStep? CurrentStep =>
        CurrentStepIndex >= 0 && CurrentStepIndex < _steps.Count
            ? _steps[CurrentStepIndex]
            : null;

    private WorkflowInstance()
    {
    }

    private WorkflowInstance(
        Guid id,
        string name,
        string? scenarioKey,
        IEnumerable<WorkflowStepDefinition> stepDefinitions,
        string actor)
    {
        var now = DateTimeOffset.UtcNow;
        Id = id;
        Name = name;
        ScenarioKey = scenarioKey;
        Status = WorkflowStatus.Pending;
        CurrentStepIndex = 0;
        CreatedAt = now;
        UpdatedAt = now;

        var order = 0;
        foreach (var def in stepDefinitions)
        {
            if (string.IsNullOrWhiteSpace(def.Name))
            {
                throw new DomainException("Workflow step name cannot be empty.");
            }

            _steps.Add(new WorkflowStep(
                id,
                order++,
                def.Name.Trim(),
                def.Type,
                def.DefaultRiskTier,
                string.IsNullOrWhiteSpace(def.TargetEnvironment) ? "staging" : def.TargetEnvironment.Trim()));
        }

        if (_steps.Count == 0)
        {
            throw new DomainException("A workflow instance must contain at least one step.");
        }

        RecordAudit(
            actor,
            "WorkflowCreated",
            WorkflowStatus.Pending,
            WorkflowStatus.Pending,
            $"Workflow '{Name}' created with {_steps.Count} step(s).",
            now);
    }

    public static WorkflowInstance Create(
        string name,
        IEnumerable<WorkflowStepDefinition> steps,
        string? scenarioKey = null,
        string actor = "system")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Workflow name cannot be empty.");
        }

        ArgumentNullException.ThrowIfNull(steps);

        return new WorkflowInstance(
            Guid.NewGuid(),
            name.Trim(),
            string.IsNullOrWhiteSpace(scenarioKey) ? null : scenarioKey.Trim(),
            steps,
            actor);
    }

    public void Start(string actor = "system")
    {
        EnsureStatus(WorkflowStatus.Pending, nameof(Start));

        var now = DateTimeOffset.UtcNow;
        var from = Status;
        Status = WorkflowStatus.Running;
        UpdatedAt = now;

        CurrentStep!.MarkRunning(now);

        RecordAudit(
            actor,
            "WorkflowStarted",
            from,
            Status,
            $"Started execution at step 0 ('{CurrentStep.Name}').",
            now);
    }

    public PolicyDecision SubmitProposalAndEvaluatePolicy(
        ActionProposal? proposal,
        IPolicyEvaluator policyEvaluator,
        string actor = "planner")
    {
        EnsureStatus(WorkflowStatus.Running, nameof(SubmitProposalAndEvaluatePolicy));
        ArgumentNullException.ThrowIfNull(policyEvaluator);

        var step = CurrentStep ?? throw new DomainException("No active step available for policy evaluation.");
        var now = DateTimeOffset.UtcNow;

        var fromRunning = Status;
        Status = WorkflowStatus.EvaluatingPolicy;
        UpdatedAt = now;
        step.MarkEvaluatingPolicy(proposal);

        RecordAudit(
            actor,
            "ProposalSubmitted",
            fromRunning,
            Status,
            proposal is null
                ? $"Empty proposal submitted for step '{step.Name}'."
                : $"Proposal '{proposal.ActionName}' (Risk={proposal.RiskTier}, Env={proposal.TargetEnvironment}) submitted for step '{step.Name}'.",
            now);

        var decision = policyEvaluator.Evaluate(new PolicyEvaluationContext(
            Id,
            Name,
            CurrentStepIndex,
            step.Name,
            step.Type,
            proposal));

        var evalTime = DateTimeOffset.UtcNow;
        var fromEvaluating = Status;
        step.ApplyPolicyDecision(decision);

        Status = decision.Verdict switch
        {
            PolicyVerdict.Allow => WorkflowStatus.ExecutingStep,
            PolicyVerdict.RequireApproval => WorkflowStatus.AwaitingApproval,
            PolicyVerdict.Deny => WorkflowStatus.BlockedByPolicy,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision.Verdict, null)
        };

        UpdatedAt = evalTime;

        if (Status == WorkflowStatus.BlockedByPolicy)
        {
            CompletedAt = evalTime;
            step.MarkBlocked(evalTime, decision.Reason);
        }

        RecordAudit(
            "policy-engine",
            "PolicyEvaluated",
            fromEvaluating,
            Status,
            $"Verdict={decision.Verdict} Rule='{decision.RuleName}': {decision.Reason}",
            evalTime);

        return decision;
    }

    public void Approve(string operatorName, string comment)
    {
        EnsureStatus(WorkflowStatus.AwaitingApproval, nameof(Approve));

        if (string.IsNullOrWhiteSpace(operatorName))
        {
            throw new DomainException("Operator name is required to approve a workflow.");
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new DomainException("An audit comment is required when approving a workflow.");
        }

        var now = DateTimeOffset.UtcNow;
        var from = Status;
        Status = WorkflowStatus.ExecutingStep;
        UpdatedAt = now;

        CurrentStep!.MarkApproved();

        RecordAudit(
            $"operator:{operatorName.Trim()}",
            "ApprovalGranted",
            from,
            Status,
            $"Approved step '{CurrentStep.Name}': {comment.Trim()}",
            now);
    }

    public void Reject(string operatorName, string comment)
    {
        EnsureStatus(WorkflowStatus.AwaitingApproval, nameof(Reject));

        if (string.IsNullOrWhiteSpace(operatorName))
        {
            throw new DomainException("Operator name is required to reject a workflow.");
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new DomainException("An audit comment is required when rejecting a workflow.");
        }

        var now = DateTimeOffset.UtcNow;
        var from = Status;
        Status = WorkflowStatus.Rejected;
        UpdatedAt = now;
        CompletedAt = now;

        CurrentStep!.MarkRejected(now, comment.Trim());

        RecordAudit(
            $"operator:{operatorName.Trim()}",
            "ApprovalRejected",
            from,
            Status,
            $"Rejected step '{CurrentStep.Name}': {comment.Trim()}",
            now);
    }

    public void CompleteCurrentStep(string? outputJson = null, string actor = "worker")
    {
        EnsureStatus(WorkflowStatus.ExecutingStep, nameof(CompleteCurrentStep));

        var step = CurrentStep ?? throw new DomainException("No active step to complete.");
        var now = DateTimeOffset.UtcNow;
        var from = Status;

        step.MarkCompleted(now, outputJson);
        UpdatedAt = now;

        if (CurrentStepIndex + 1 < _steps.Count)
        {
            CurrentStepIndex++;
            Status = WorkflowStatus.Running;
            CurrentStep!.MarkRunning(now);

            RecordAudit(
                actor,
                "StepCompleted",
                from,
                Status,
                $"Completed step '{step.Name}'. Advancing to step {CurrentStepIndex} ('{CurrentStep.Name}').",
                now);
        }
        else
        {
            Status = WorkflowStatus.Completed;
            CompletedAt = now;

            RecordAudit(
                actor,
                "WorkflowCompleted",
                from,
                Status,
                $"Completed final step '{step.Name}'. Workflow finished.",
                now);
        }
    }

    public void FailCurrentStep(string errorMessage, string actor = "worker")
    {
        if (Status is not (WorkflowStatus.ExecutingStep or WorkflowStatus.Running))
        {
            throw new InvalidWorkflowTransitionException(Id, Status, nameof(FailCurrentStep));
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new DomainException("Error message cannot be empty when failing a step.");
        }

        var step = CurrentStep ?? throw new DomainException("No active step to fail.");
        var now = DateTimeOffset.UtcNow;
        var from = Status;

        step.MarkFailed(now, errorMessage.Trim());
        Status = WorkflowStatus.Failed;
        UpdatedAt = now;
        CompletedAt = now;

        RecordAudit(
            actor,
            "WorkflowFailed",
            from,
            Status,
            $"Step '{step.Name}' failed: {errorMessage.Trim()}",
            now);
    }

    private void EnsureStatus(WorkflowStatus expectedStatus, string actionName)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidWorkflowTransitionException(Id, Status, actionName);
        }
    }

    private void RecordAudit(
        string actor,
        string eventType,
        WorkflowStatus fromStatus,
        WorkflowStatus toStatus,
        string details,
        DateTimeOffset timestamp)
    {
        _auditEntries.Add(new WorkflowAuditEntry(
            Id,
            actor,
            eventType,
            fromStatus,
            toStatus,
            details,
            timestamp));
    }
}
