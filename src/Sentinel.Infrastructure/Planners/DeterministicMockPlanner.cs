using Microsoft.Extensions.Options;
using Sentinel.Application.Abstractions;
using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Infrastructure.Planners;

public sealed class DeterministicMockPlanner : IAgentPlanner
{
    private readonly AgentPlannerOptions _options;

    public DeterministicMockPlanner(IOptions<AgentPlannerOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ActionProposal> ProposeActionAsync(
        AgentPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_options.SimulatedLatencyMs > 0)
        {
            await Task.Delay(_options.SimulatedLatencyMs, cancellationToken);
        }

        var scenario = request.ScenarioKey?.Trim().ToLowerInvariant();
        var env = string.IsNullOrWhiteSpace(request.TargetEnvironment) ? "staging" : request.TargetEnvironment;

        return scenario switch
        {
            "auto-triage" => new ActionProposal(
                ActionName: request.StepType == StepType.Diagnostic
                    ? "CollectLatencyDiagnostics"
                    : "PublishTriageReport",
                TargetEnvironment: env,
                RiskTier: RiskTier.Low,
                PayloadJson: """{"service":"checkout-api","windowMinutes":15,"readOnly":true}""",
                Rationale: "Latency spike detected in checkout-api; proposing read-only diagnostic collection and report."),

            "prod-scale" => new ActionProposal(
                ActionName: request.StepType == StepType.Diagnostic
                    ? "InspectClusterLoad"
                    : "ScaleDatabaseCluster",
                TargetEnvironment: request.StepType == StepType.Diagnostic ? env : "production",
                RiskTier: request.StepType == StepType.Diagnostic ? RiskTier.Low : RiskTier.High,
                PayloadJson: request.StepType == StepType.Diagnostic
                    ? """{"cluster":"sentinel-pg-primary","metric":"cpu_utilization"}"""
                    : """{"cluster":"sentinel-pg-primary","targetReplicas":4,"reason":"Sustained CPU > 88%"}""",
                Rationale: request.StepType == StepType.Diagnostic
                    ? "Collecting primary cluster telemetry before remediation."
                    : "Sustained CPU saturation on production database primary; proposing horizontal read-replica scale-out."),

            "blocked-delete" => new ActionProposal(
                ActionName: "DeleteProductionVolume",
                TargetEnvironment: "production",
                RiskTier: RiskTier.Critical,
                PayloadJson: """{"volumeId":"vol-prod-0192","force":true}""",
                Rationale: "Disk usage alert triggered automated cleanup proposal targeting attached volume."),

            _ => new ActionProposal(
                ActionName: request.StepType switch
                {
                    StepType.Diagnostic => "CollectServiceDiagnostics",
                    StepType.AiPlanner => "SynthesizeRemediationPlan",
                    _ => "ExecuteOperationalAction"
                },
                TargetEnvironment: env,
                RiskTier: request.DefaultRiskTier,
                PayloadJson: $$"""{"workflowId":"{{request.WorkflowId}}","step":"{{request.StepName}}"}""",
                Rationale: $"Deterministic proposal generated for step '{request.StepName}' in '{env}'.")
        };
    }
}
