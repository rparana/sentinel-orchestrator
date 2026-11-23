using Sentinel.Domain.Enums;
using Sentinel.Domain.Policies;
using Sentinel.Domain.ValueObjects;
using Xunit;

namespace Sentinel.Domain.Tests;

public sealed class DeterministicPolicyEngineTests
{
    private readonly DeterministicPolicyEngine _sut = new();

    [Fact]
    public void Evaluate_NullProposal_ReturnsDeny()
    {
        var decision = _sut.Evaluate(CreateContext(proposal: null));

        Assert.Equal(PolicyVerdict.Deny, decision.Verdict);
        Assert.Equal("EmptyProposalGuard", decision.RuleName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_EmptyActionName_ReturnsDeny(string actionName)
    {
        var proposal = new ActionProposal(actionName, "staging", RiskTier.Low);

        var decision = _sut.Evaluate(CreateContext(proposal));

        Assert.Equal(PolicyVerdict.Deny, decision.Verdict);
        Assert.Equal("EmptyProposalGuard", decision.RuleName);
    }

    [Theory]
    [InlineData("DeleteProductionVolume")]
    [InlineData("deleteproductionvolume")]
    [InlineData("DropDatabase")]
    [InlineData("DisableFirewall")]
    public void Evaluate_HardBlockedDestructiveAction_ReturnsDeny(string actionName)
    {
        var proposal = new ActionProposal(actionName, "staging", RiskTier.Low);

        var decision = _sut.Evaluate(CreateContext(proposal));

        Assert.Equal(PolicyVerdict.Deny, decision.Verdict);
        Assert.Equal("HardBlockedDestructiveAction", decision.RuleName);
    }

    [Theory]
    [InlineData("staging")]
    [InlineData("dev")]
    [InlineData("production")]
    public void Evaluate_CriticalRisk_ReturnsRequireApproval(string environment)
    {
        var proposal = new ActionProposal("RestartCluster", environment, RiskTier.Critical);

        var decision = _sut.Evaluate(CreateContext(proposal));

        Assert.Equal(PolicyVerdict.RequireApproval, decision.Verdict);
        Assert.Equal("CriticalRiskRequiresApproval", decision.RuleName);
    }

    [Theory]
    [InlineData("production")]
    [InlineData("prod")]
    [InlineData("PRODUCTION")]
    public void Evaluate_HighRiskInProduction_ReturnsRequireApproval(string environment)
    {
        var proposal = new ActionProposal("ScaleDatabaseCluster", environment, RiskTier.High);

        var decision = _sut.Evaluate(CreateContext(proposal));

        Assert.Equal(PolicyVerdict.RequireApproval, decision.Verdict);
        Assert.Equal("ProductionHighRiskRequiresApproval", decision.RuleName);
    }

    [Theory]
    [InlineData("staging", RiskTier.High)]
    [InlineData("dev", RiskTier.High)]
    [InlineData("production", RiskTier.Low)]
    [InlineData("production", RiskTier.Medium)]
    [InlineData("staging", RiskTier.Low)]
    public void Evaluate_SafeBoundaries_ReturnsAllow(string environment, RiskTier riskTier)
    {
        var proposal = new ActionProposal("CollectDiagnostics", environment, riskTier);

        var decision = _sut.Evaluate(CreateContext(proposal));

        Assert.Equal(PolicyVerdict.Allow, decision.Verdict);
        Assert.Equal("SafeBoundaryAllow", decision.RuleName);
    }

    private static PolicyEvaluationContext CreateContext(ActionProposal? proposal) =>
        new(
            Guid.NewGuid(),
            "Test Workflow",
            0,
            "Step 1",
            StepType.ActionExecutor,
            proposal);
}
