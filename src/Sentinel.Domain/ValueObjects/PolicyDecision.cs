using Sentinel.Domain.Enums;

namespace Sentinel.Domain.ValueObjects;

public sealed record PolicyDecision(
    PolicyVerdict Verdict,
    string RuleName,
    string Reason,
    DateTimeOffset EvaluatedAt);
