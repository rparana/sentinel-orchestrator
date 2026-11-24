using Sentinel.Application.Abstractions;
using Sentinel.Domain.Enums;

namespace Sentinel.Infrastructure.Notifications;

public sealed class NoOpWorkflowNotificationPublisher : IWorkflowNotificationPublisher
{
    public Task PublishWorkflowUpdatedAsync(
        Guid workflowId,
        WorkflowStatus status,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishApprovalRequestedAsync(
        Guid workflowId,
        int stepIndex,
        string stepName,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
