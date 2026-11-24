using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Application.Workflows;

public sealed record ScenarioDefinition(
    string Key,
    string Title,
    string Description,
    RiskTier ExpectedRisk,
    IReadOnlyList<WorkflowStepDefinition> Steps);

public static class ScenarioCatalog
{
    private static readonly IReadOnlyList<ScenarioDefinition> Scenarios =
    [
        new(
            Key: "auto-triage",
            Title: "Incident Triage & Diagnostics",
            Description: "Read-only diagnostic analysis and triage report (Auto-Allowed by Policy).",
            ExpectedRisk: RiskTier.Low,
            Steps:
            [
                new WorkflowStepDefinition("Collect Latency Logs", StepType.Diagnostic, RiskTier.Low, "staging"),
                new WorkflowStepDefinition("Publish Triage Report", StepType.ActionExecutor, RiskTier.Low, "staging")
            ]),
        new(
            Key: "prod-scale",
            Title: "Production Database Scaling",
            Description: "High-risk production cluster scale-out requiring Human-in-the-Loop approval.",
            ExpectedRisk: RiskTier.High,
            Steps:
            [
                new WorkflowStepDefinition("Inspect Cluster Load", StepType.Diagnostic, RiskTier.Low, "production"),
                new WorkflowStepDefinition("Scale Database Cluster", StepType.ActionExecutor, RiskTier.High, "production")
            ]),
        new(
            Key: "blocked-delete",
            Title: "Destructive Storage Deletion",
            Description: "Proposes DeleteProductionVolume, which is hard-blocked by Policy guardrails.",
            ExpectedRisk: RiskTier.Critical,
            Steps:
            [
                new WorkflowStepDefinition("Delete Production Volume", StepType.ActionExecutor, RiskTier.Critical, "production")
            ])
    ];

    public static IReadOnlyList<ScenarioDefinition> GetAll() => Scenarios;

    public static ScenarioDefinition? FindByKey(string scenarioKey) =>
        Scenarios.FirstOrDefault(s => string.Equals(s.Key, scenarioKey?.Trim(), StringComparison.OrdinalIgnoreCase));
}
