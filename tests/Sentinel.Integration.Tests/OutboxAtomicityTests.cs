using Microsoft.EntityFrameworkCore;
using Sentinel.Domain.Entities;
using Sentinel.Domain.Enums;
using Sentinel.Domain.Events;
using Sentinel.Domain.ValueObjects;
using Sentinel.Integration.Tests.Fixtures;
using Xunit;

namespace Sentinel.Integration.Tests;

[Collection(SentinelContainersCollection.Name)]
public sealed class OutboxAtomicityTests
{
    private readonly SentinelContainersFixture _fixture;

    public OutboxAtomicityTests(SentinelContainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsWorkflowAndOutboxMessageAtomically()
    {
        var workflow = WorkflowInstance.Create(
            "Atomicity Test Workflow",
            [new WorkflowStepDefinition("Diagnose", StepType.Diagnostic, RiskTier.Low, "staging")],
            scenarioKey: "auto-triage");

        workflow.Start();

        await using (var db = _fixture.CreateDbContext())
        {
            db.Workflows.Add(workflow);
            await db.SaveChangesAsync();
        }

        await using var verifyDb = _fixture.CreateDbContext();
        var savedWorkflow = await verifyDb.Workflows
            .Include(w => w.Steps)
            .Include(w => w.AuditEntries)
            .FirstOrDefaultAsync(w => w.Id == workflow.Id);

        Assert.NotNull(savedWorkflow);
        Assert.Equal(WorkflowStatus.Running, savedWorkflow.Status);
        Assert.Single(savedWorkflow.Steps);
        Assert.Equal(2, savedWorkflow.AuditEntries.Count);

        var outboxMessages = (await verifyDb.OutboxMessages
                .Where(o => o.EventType == nameof(WorkflowStepReadyForPlanningDomainEvent))
                .ToListAsync())
            .Where(o => o.PayloadJson.Contains(workflow.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var outbox = Assert.Single(outboxMessages);
        Assert.Null(outbox.ProcessedAt);
    }
}
