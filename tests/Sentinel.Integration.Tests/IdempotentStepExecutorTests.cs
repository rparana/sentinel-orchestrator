using Microsoft.Extensions.Logging.Abstractions;
using Sentinel.Application.Abstractions;
using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;
using Sentinel.Infrastructure.Execution;
using Sentinel.Integration.Tests.Fixtures;
using Xunit;

namespace Sentinel.Integration.Tests;

[Collection(SentinelContainersCollection.Name)]
public sealed class IdempotentStepExecutorTests
{
    private readonly SentinelContainersFixture _fixture;

    public IdempotentStepExecutorTests(SentinelContainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_WithSameWorkflowStepAndAttempt_DeduplicatesInRedis()
    {
        await using var redis = _fixture.CreateRedisConnection();
        var executor = new IdempotentStepExecutor(redis, NullLogger<IdempotentStepExecutor>.Instance);

        var workflowId = Guid.NewGuid();
        var proposal = new ActionProposal("CollectDiagnostics", "staging", RiskTier.Low);

        var first = await executor.ExecuteAsync(new StepExecutionRequest(
            workflowId, "Idempotency Test", 0, 1, "Step 1", StepType.Diagnostic, proposal));

        var duplicate = await executor.ExecuteAsync(new StepExecutionRequest(
            workflowId, "Idempotency Test", 0, 1, "Step 1", StepType.Diagnostic, proposal));

        var retryAttempt = await executor.ExecuteAsync(new StepExecutionRequest(
            workflowId, "Idempotency Test", 0, 2, "Step 1", StepType.Diagnostic, proposal));

        Assert.True(first.Succeeded);
        Assert.False(first.WasDeduplicated);

        Assert.True(duplicate.Succeeded);
        Assert.True(duplicate.WasDeduplicated);

        Assert.True(retryAttempt.Succeeded);
        Assert.False(retryAttempt.WasDeduplicated);
    }
}
