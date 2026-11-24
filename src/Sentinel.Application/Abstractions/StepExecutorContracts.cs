using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Application.Abstractions;

public sealed record StepExecutionRequest(
    Guid WorkflowId,
    string WorkflowName,
    int StepIndex,
    int Attempt,
    string StepName,
    StepType StepType,
    ActionProposal Proposal);

public sealed record StepExecutionResult(
    bool Succeeded,
    string? OutputJson = null,
    string? ErrorMessage = null,
    bool WasDeduplicated = false);

public interface IStepExecutor
{
    Task<StepExecutionResult> ExecuteAsync(
        StepExecutionRequest request,
        CancellationToken cancellationToken = default);
}
