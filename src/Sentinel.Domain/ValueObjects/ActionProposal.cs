using Sentinel.Domain.Enums;

namespace Sentinel.Domain.ValueObjects;

public sealed record ActionProposal(
    string ActionName,
    string TargetEnvironment,
    RiskTier RiskTier,
    string PayloadJson = "{}",
    string Rationale = "");
