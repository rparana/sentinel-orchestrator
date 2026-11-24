using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sentinel.Application.Abstractions;
using StackExchange.Redis;

namespace Sentinel.Infrastructure.Execution;

public sealed class IdempotentStepExecutor : IStepExecutor
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<IdempotentStepExecutor> _logger;

    public IdempotentStepExecutor(
        IConnectionMultiplexer redis,
        ILogger<IdempotentStepExecutor> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public static string BuildIdempotencyKey(Guid workflowId, int stepIndex, int attempt) =>
        $"sentinel:idempotency:{workflowId}:{stepIndex}:{attempt}";

    public async Task<StepExecutionResult> ExecuteAsync(
        StepExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var key = BuildIdempotencyKey(request.WorkflowId, request.StepIndex, request.Attempt);

        // Acquire atomic idempotency lock in Redis
        var acquired = await db.StringSetAsync(
            key,
            "IN_PROGRESS",
            IdempotencyTtl,
            When.NotExists);

        if (!acquired)
        {
            var existingValue = await db.StringGetAsync(key);
            _logger.LogInformation(
                "Deduplicated step execution for key {IdempotencyKey} (existing state: {State})",
                key,
                existingValue.HasValue ? existingValue.ToString() : "unknown");

            return new StepExecutionResult(
                Succeeded: true,
                OutputJson: existingValue.HasValue && existingValue.ToString() != "IN_PROGRESS"
                    ? existingValue.ToString()
                    : """{"deduplicated":true}""",
                WasDeduplicated: true);
        }

        var outputJson = JsonSerializer.Serialize(new
        {
            executedAction = request.Proposal.ActionName,
            environment = request.Proposal.TargetEnvironment,
            workflowId = request.WorkflowId,
            stepIndex = request.StepIndex,
            attempt = request.Attempt,
            executedAtUtc = DateTimeOffset.UtcNow
        });

        await db.StringSetAsync(key, outputJson, IdempotencyTtl);

        _logger.LogInformation(
            "Executed action {ActionName} for workflow {WorkflowId} step {StepIndex} (attempt {Attempt})",
            request.Proposal.ActionName,
            request.WorkflowId,
            request.StepIndex,
            request.Attempt);

        return new StepExecutionResult(
            Succeeded: true,
            OutputJson: outputJson,
            WasDeduplicated: false);
    }
}
