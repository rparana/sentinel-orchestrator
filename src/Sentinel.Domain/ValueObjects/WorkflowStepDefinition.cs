using Sentinel.Domain.Enums;

namespace Sentinel.Domain.ValueObjects;

public sealed record WorkflowStepDefinition(
    string Name,
    StepType Type,
    RiskTier DefaultRiskTier = RiskTier.Low,
    string TargetEnvironment = "staging");
