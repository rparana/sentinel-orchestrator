using Microsoft.EntityFrameworkCore;
using Sentinel.Domain.Entities;
using Sentinel.Domain.Enums;
using Sentinel.Domain.Policies;
using Sentinel.Domain.ValueObjects;
using Sentinel.Integration.Tests.Fixtures;
using Xunit;

namespace Sentinel.Integration.Tests;

[Collection(SentinelContainersCollection.Name)]
public sealed class OptimisticConcurrencyTests
{
    private readonly SentinelContainersFixture _fixture;

    public OptimisticConcurrencyTests(SentinelContainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConcurrentApprovals_SecondSaveThrowsDbUpdateConcurrencyException()
    {
        var workflow = WorkflowInstance.Create(
            "Concurrency Test Workflow",
            [new WorkflowStepDefinition("Scale DB", StepType.ActionExecutor, RiskTier.High, "production")],
            scenarioKey: "prod-scale");

        workflow.Start();
        workflow.SubmitProposalAndEvaluatePolicy(
            new ActionProposal("ScaleDatabaseCluster", "production", RiskTier.High),
            new DeterministicPolicyEngine());

        await using (var setupDb = _fixture.CreateDbContext())
        {
            setupDb.Workflows.Add(workflow);
            await setupDb.SaveChangesAsync();
        }

        await using var db1 = _fixture.CreateDbContext();
        await using var db2 = _fixture.CreateDbContext();

        var w1 = await db1.Workflows
            .Include(w => w.Steps)
            .FirstAsync(w => w.Id == workflow.Id);

        var w2 = await db2.Workflows
            .Include(w => w.Steps)
            .FirstAsync(w => w.Id == workflow.Id);

        w1.Approve("operator-1", "First operator approved");
        await db1.SaveChangesAsync();

        w2.Approve("operator-2", "Second operator attempted simultaneous approval");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }
}
