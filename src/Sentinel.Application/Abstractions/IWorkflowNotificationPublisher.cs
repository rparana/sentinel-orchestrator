using Sentinel.Domain.Enums;

namespace Sentinel.Application.Abstractions;

public interface IWorkflowNotificationPublisher
{
    Task PublishWorkflowUpdatedAsync(
        Guid workflowId,
        WorkflowStatus status,
        CancellationToken cancellationToken = default);

    Task PublishApprovalRequestedAsync(
        Guid workflowId,
        int stepIndex,
        string stepName,
        CancellationToken cancellationToken = default);
}
