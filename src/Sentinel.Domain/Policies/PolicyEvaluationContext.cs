using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Domain.Policies;

public sealed record PolicyEvaluationContext(
    Guid WorkflowId,
    string WorkflowName,
    int StepIndex,
    string StepName,
    StepType StepType,
    ActionProposal? Proposal);
