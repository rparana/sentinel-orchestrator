using Sentinel.Domain.ValueObjects;

namespace Sentinel.Domain.Policies;

public interface IPolicyEvaluator
{
    PolicyDecision Evaluate(PolicyEvaluationContext context);
}
