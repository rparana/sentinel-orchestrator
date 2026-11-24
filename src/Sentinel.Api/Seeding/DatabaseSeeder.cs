using Microsoft.EntityFrameworkCore;
using Sentinel.Application.Workflows;
using Sentinel.Domain.Entities;
using Sentinel.Domain.Enums;
using Sentinel.Domain.Policies;
using Sentinel.Domain.ValueObjects;
using Sentinel.Infrastructure.Persistence;

namespace Sentinel.Api.Seeding;

public static class DatabaseSeeder
{
    public static async Task MigrateAndSeedAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var policyEvaluator = scope.ServiceProvider.GetRequiredService<IPolicyEvaluator>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseSeeder");

        await db.Database.MigrateAsync(cancellationToken);

        if (await db.Workflows.AnyAsync(cancellationToken))
        {
            return;
        }

        logger.LogInformation("Seeding initial showcase workflow scenarios...");

        var seeded = new List<WorkflowInstance>
        {
            BuildCompletedAutoTriage(policyEvaluator),
            BuildAwaitingApprovalProdScale(policyEvaluator),
            BuildBlockedDestructiveDelete(policyEvaluator)
        };

        foreach (var workflow in seeded)
        {
            // Historical showcase workflows are already at their target demo state
            workflow.ClearDomainEvents();
        }

        await db.Workflows.AddRangeAsync(seeded, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} showcase workflows.", seeded.Count);
    }

    private static WorkflowInstance BuildCompletedAutoTriage(IPolicyEvaluator policyEvaluator)
    {
        var def = ScenarioCatalog.FindByKey("auto-triage")!;
        var wf = WorkflowInstance.Create(def.Title, def.Steps, def.Key, "seeder");
        wf.Start("seeder");

        wf.SubmitProposalAndEvaluatePolicy(
            new ActionProposal(
                "CollectLatencyDiagnostics",
                "staging",
                RiskTier.Low,
                """{"service":"checkout-api","windowMinutes":15,"readOnly":true}""",
                "Read-only diagnostic analysis of checkout-api p99 latency."),
            policyEvaluator,
            "seeder");
        wf.CompleteCurrentStep("""{"p99LatencyMs":42,"anomalies":0}""", "seeder");

        wf.SubmitProposalAndEvaluatePolicy(
            new ActionProposal(
                "PublishTriageReport",
                "staging",
                RiskTier.Low,
                """{"channel":"#sre-triage","severity":"info"}""",
                "Publish diagnostic summary to triage feed."),
            policyEvaluator,
            "seeder");
        wf.CompleteCurrentStep("""{"reportId":"rep-seed-001"}""", "seeder");

        return wf;
    }

    private static WorkflowInstance BuildAwaitingApprovalProdScale(IPolicyEvaluator policyEvaluator)
    {
        var def = ScenarioCatalog.FindByKey("prod-scale")!;
        var wf = WorkflowInstance.Create(def.Title, def.Steps, def.Key, "seeder");
        wf.Start("seeder");

        wf.SubmitProposalAndEvaluatePolicy(
            new ActionProposal(
                "InspectClusterLoad",
                "production",
                RiskTier.Low,
                """{"cluster":"sentinel-pg-primary","metric":"cpu_utilization"}""",
                "Collecting primary database CPU and connection pool metrics."),
            policyEvaluator,
            "seeder");
        wf.CompleteCurrentStep("""{"cpuPercent":91.4,"activeConnections":184}""", "seeder");

        wf.SubmitProposalAndEvaluatePolicy(
            new ActionProposal(
                "ScaleDatabaseCluster",
                "production",
                RiskTier.High,
                """{"cluster":"sentinel-pg-primary","targetReplicas":4,"reason":"Sustained CPU > 88%"}""",
                "Proposing horizontal read-replica scale-out on production cluster."),
            policyEvaluator,
            "seeder");

        return wf;
    }

    private static WorkflowInstance BuildBlockedDestructiveDelete(IPolicyEvaluator policyEvaluator)
    {
        var def = ScenarioCatalog.FindByKey("blocked-delete")!;
        var wf = WorkflowInstance.Create(def.Title, def.Steps, def.Key, "seeder");
        wf.Start("seeder");

        wf.SubmitProposalAndEvaluatePolicy(
            new ActionProposal(
                "DeleteProductionVolume",
                "production",
                RiskTier.Critical,
                """{"volumeId":"vol-prod-0192","force":true}""",
                "Disk usage alert triggered automated cleanup proposal."),
            policyEvaluator,
            "seeder");

        return wf;
    }
}
