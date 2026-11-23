using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Domain.Policies;

public sealed class DeterministicPolicyEngine : IPolicyEvaluator
{
    private static readonly HashSet<string> HardBlockedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "DeleteProductionVolume",
        "DropDatabase",
        "DisableFirewall"
    };

    private static readonly HashSet<string> ProductionEnvironments = new(StringComparer.OrdinalIgnoreCase)
    {
        "prod",
        "production"
    };

    public PolicyDecision Evaluate(PolicyEvaluationContext context)
    {
        var now = DateTimeOffset.UtcNow;
        var proposal = context.Proposal;

        if (proposal is null || string.IsNullOrWhiteSpace(proposal.ActionName))
        {
            return new PolicyDecision(
                PolicyVerdict.Deny,
                "EmptyProposalGuard",
                "Action proposal is missing or does not specify an action name.",
                now);
        }

        var normalizedAction = proposal.ActionName.Trim();

        if (HardBlockedActions.Contains(normalizedAction))
        {
            return new PolicyDecision(
                PolicyVerdict.Deny,
                "HardBlockedDestructiveAction",
                $"Action '{normalizedAction}' is permanently blocked by deterministic safety policy.",
                now);
        }

        if (proposal.RiskTier == RiskTier.Critical)
        {
            return new PolicyDecision(
                PolicyVerdict.RequireApproval,
                "CriticalRiskRequiresApproval",
                $"Action '{normalizedAction}' has Critical risk tier and requires human approval.",
                now);
        }

        if (proposal.RiskTier == RiskTier.High && IsProductionEnvironment(proposal.TargetEnvironment))
        {
            return new PolicyDecision(
                PolicyVerdict.RequireApproval,
                "ProductionHighRiskRequiresApproval",
                $"High-risk action '{normalizedAction}' targeting '{proposal.TargetEnvironment}' requires human approval.",
                now);
        }

        return new PolicyDecision(
            PolicyVerdict.Allow,
            "SafeBoundaryAllow",
            $"Action '{normalizedAction}' (Risk={proposal.RiskTier}, Env={proposal.TargetEnvironment}) is within safe policy boundaries.",
            now);
    }

    private static bool IsProductionEnvironment(string? environment) =>
        !string.IsNullOrWhiteSpace(environment) &&
        ProductionEnvironments.Contains(environment.Trim());
}
