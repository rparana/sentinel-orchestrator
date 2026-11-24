using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.Domain.Entities;
using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;
using Sentinel.Infrastructure.Outbox;
using Sentinel.Infrastructure.Persistence;
using Sentinel.Integration.Tests.Fixtures;
using Xunit;

namespace Sentinel.Integration.Tests;

[Collection(SentinelContainersCollection.Name)]
public sealed class OutboxWorkerEndToEndTests
{
    private readonly SentinelContainersFixture _fixture;

    public OutboxWorkerEndToEndTests(SentinelContainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OutboxWorker_AutoAllowedWorkflow_CompletesEndToEnd()
    {
        await using var provider = _fixture.BuildServiceProvider();
        var worker = provider.GetRequiredService<OutboxProcessorWorker>();

        var workflow = WorkflowInstance.Create(
            "Auto Triage E2E",
            [
                new WorkflowStepDefinition("Analyze Latency", StepType.Diagnostic, RiskTier.Low, "staging"),
                new WorkflowStepDefinition("Publish Report", StepType.ActionExecutor, RiskTier.Low, "staging")
            ],
            scenarioKey: "auto-triage");

        workflow.Start();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            db.Workflows.Add(workflow);
            await db.SaveChangesAsync();
        }

        await DrainOutboxAsync(worker);

        await using var verifyDb = _fixture.CreateDbContext();
        var completed = await verifyDb.Workflows
            .Include(w => w.Steps)
            .Include(w => w.AuditEntries)
            .FirstAsync(w => w.Id == workflow.Id);

        Assert.Equal(WorkflowStatus.Completed, completed.Status);
        Assert.All(completed.Steps, s => Assert.Equal(StepStatus.Completed, s.Status));
    }

    [Fact]
    public async Task OutboxWorker_HighRiskProductionWorkflow_PausesForApprovalAndResumesAfterApprove()
    {
        await using var provider = _fixture.BuildServiceProvider();
        var worker = provider.GetRequiredService<OutboxProcessorWorker>();

        var workflow = WorkflowInstance.Create(
            "Prod Scale E2E",
            [new WorkflowStepDefinition("Scale Cluster", StepType.ActionExecutor, RiskTier.High, "production")],
            scenarioKey: "prod-scale");

        workflow.Start();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            db.Workflows.Add(workflow);
            await db.SaveChangesAsync();
        }

        await DrainOutboxAsync(worker);

        await using (var approvalDb = _fixture.CreateDbContext())
        {
            var paused = await approvalDb.Workflows
                .Include(w => w.Steps)
                .FirstAsync(w => w.Id == workflow.Id);

            Assert.Equal(WorkflowStatus.AwaitingApproval, paused.Status);

            paused.Approve("rafael", "Approved for integration test");
            await approvalDb.SaveChangesAsync();
        }

        await DrainOutboxAsync(worker);

        await using var verifyDb = _fixture.CreateDbContext();
        var resumed = await verifyDb.Workflows
            .Include(w => w.Steps)
            .FirstAsync(w => w.Id == workflow.Id);

        Assert.Equal(WorkflowStatus.Completed, resumed.Status);
        Assert.Equal(StepStatus.Completed, resumed.Steps[0].Status);
    }

    private static async Task DrainOutboxAsync(OutboxProcessorWorker worker)
    {
        for (var i = 0; i < 15; i++)
        {
            var count = await worker.ProcessBatchAsync();
            if (count == 0)
            {
                return;
            }
        }
    }
}
