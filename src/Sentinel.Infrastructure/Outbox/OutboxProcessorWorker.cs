using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinel.Application.Abstractions;
using Sentinel.Domain.Enums;
using Sentinel.Domain.Events;
using Sentinel.Domain.Policies;
using Sentinel.Infrastructure.Persistence;

namespace Sentinel.Infrastructure.Outbox;

public sealed class OutboxProcessorWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorWorker> _logger;
    private readonly OutboxOptions _options;

    public OutboxProcessorWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorWorker started (polling every {IntervalMs}ms).", _options.PollingIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(_options.PollingIntervalMs, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in OutboxProcessorWorker loop.");
                await Task.Delay(Math.Max(_options.PollingIntervalMs, 1000), stoppingToken);
            }
        }
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var planner = scope.ServiceProvider.GetRequiredService<IAgentPlanner>();
        var policyEvaluator = scope.ServiceProvider.GetRequiredService<IPolicyEvaluator>();
        var executor = scope.ServiceProvider.GetRequiredService<IStepExecutor>();
        var notifier = scope.ServiceProvider.GetRequiredService<IWorkflowNotificationPublisher>();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var messages = await db.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT id, occurred_at, event_type, payload_json, processed_at, retry_count, error
                FROM outbox_messages
                WHERE processed_at IS NULL
                ORDER BY occurred_at
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        foreach (var message in messages)
        {
            try
            {
                await DispatchMessageAsync(db, planner, policyEvaluator, executor, notifier, message, cancellationToken);
                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

                _logger.LogWarning(
                    ex,
                    "Failed processing outbox message {MessageId} ({EventType}), retry {RetryCount}/{MaxRetries}.",
                    message.Id,
                    message.EventType,
                    message.RetryCount,
                    _options.MaxRetries);

                if (message.RetryCount >= _options.MaxRetries)
                {
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages.Count;
    }

    private static async Task DispatchMessageAsync(
        SentinelDbContext db,
        IAgentPlanner planner,
        IPolicyEvaluator policyEvaluator,
        IStepExecutor executor,
        IWorkflowNotificationPublisher notifier,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        switch (message.EventType)
        {
            case nameof(WorkflowStepReadyForPlanningDomainEvent):
            {
                var evt = Deserialize<WorkflowStepReadyForPlanningDomainEvent>(message.PayloadJson);
                var workflow = await db.Workflows
                    .Include(w => w.Steps.OrderBy(s => s.Order))
                    .FirstOrDefaultAsync(w => w.Id == evt.WorkflowInstanceId, cancellationToken);

                if (workflow is null ||
                    workflow.Status != WorkflowStatus.Running ||
                    workflow.CurrentStepIndex != evt.StepIndex ||
                    workflow.CurrentStep is null)
                {
                    return;
                }

                var step = workflow.CurrentStep;
                var proposal = await planner.ProposeActionAsync(
                    new AgentPlanRequest(
                        workflow.Id,
                        workflow.Name,
                        workflow.ScenarioKey,
                        evt.StepIndex,
                        step.Name,
                        step.Type,
                        step.DefaultRiskTier,
                        step.TargetEnvironment),
                    cancellationToken);

                workflow.SubmitProposalAndEvaluatePolicy(proposal, policyEvaluator);
                await notifier.PublishWorkflowUpdatedAsync(workflow.Id, workflow.Status, cancellationToken);
                break;
            }

            case nameof(WorkflowStepReadyForExecutionDomainEvent):
            {
                var evt = Deserialize<WorkflowStepReadyForExecutionDomainEvent>(message.PayloadJson);
                var workflow = await db.Workflows
                    .Include(w => w.Steps.OrderBy(s => s.Order))
                    .FirstOrDefaultAsync(w => w.Id == evt.WorkflowInstanceId, cancellationToken);

                if (workflow is null ||
                    workflow.Status != WorkflowStatus.ExecutingStep ||
                    workflow.CurrentStepIndex != evt.StepIndex ||
                    workflow.CurrentStep is null ||
                    workflow.CurrentStep.ProposedAction is null)
                {
                    return;
                }

                var step = workflow.CurrentStep;
                var result = await executor.ExecuteAsync(
                    new StepExecutionRequest(
                        workflow.Id,
                        workflow.Name,
                        evt.StepIndex,
                        evt.Attempt,
                        step.Name,
                        step.Type,
                        step.ProposedAction!),
                    cancellationToken);

                if (result.Succeeded)
                {
                    workflow.CompleteCurrentStep(result.OutputJson);
                }
                else
                {
                    workflow.FailCurrentStep(result.ErrorMessage ?? "Step execution failed.");
                }

                await notifier.PublishWorkflowUpdatedAsync(workflow.Id, workflow.Status, cancellationToken);
                break;
            }

            case nameof(WorkflowAwaitingApprovalDomainEvent):
            {
                var evt = Deserialize<WorkflowAwaitingApprovalDomainEvent>(message.PayloadJson);
                var stepName = await db.WorkflowSteps
                    .Where(s => s.WorkflowInstanceId == evt.WorkflowInstanceId && s.Order == evt.StepIndex)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync(cancellationToken) ?? $"Step {evt.StepIndex}";

                await notifier.PublishApprovalRequestedAsync(evt.WorkflowInstanceId, evt.StepIndex, stepName, cancellationToken);
                await notifier.PublishWorkflowUpdatedAsync(evt.WorkflowInstanceId, WorkflowStatus.AwaitingApproval, cancellationToken);
                break;
            }

            case nameof(WorkflowTerminalStateReachedDomainEvent):
            {
                var evt = Deserialize<WorkflowTerminalStateReachedDomainEvent>(message.PayloadJson);
                await notifier.PublishWorkflowUpdatedAsync(evt.WorkflowInstanceId, evt.FinalStatus, cancellationToken);
                break;
            }
        }
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException($"Unable to deserialize outbox payload as {typeof(T).Name}.");
}
