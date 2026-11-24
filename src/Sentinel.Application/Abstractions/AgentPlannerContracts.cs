using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Application.Abstractions;

public sealed record AgentPlanRequest(
    Guid WorkflowId,
    string WorkflowName,
    string? ScenarioKey,
    int StepIndex,
    string StepName,
    StepType StepType,
    RiskTier DefaultRiskTier,
    string TargetEnvironment);

public interface IAgentPlanner
{
    Task<ActionProposal> ProposeActionAsync(
        AgentPlanRequest request,
        CancellationToken cancellationToken = default);
}
