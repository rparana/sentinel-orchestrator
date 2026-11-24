using Microsoft.AspNetCore.SignalR;
using Sentinel.Application.Abstractions;
using Sentinel.Domain.Enums;

namespace Sentinel.Api.Hubs;

public sealed class SignalRWorkflowNotificationPublisher : IWorkflowNotificationPublisher
{
    private readonly IHubContext<WorkflowHub> _hubContext;

    public SignalRWorkflowNotificationPublisher(IHubContext<WorkflowHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishWorkflowUpdatedAsync(
        Guid workflowId,
        WorkflowStatus status,
        CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync(
            "WorkflowUpdated",
            new
            {
                workflowId,
                status,
                timestamp = DateTimeOffset.UtcNow
            },
            cancellationToken);

    public Task PublishApprovalRequestedAsync(
        Guid workflowId,
        int stepIndex,
        string stepName,
        CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync(
            "ApprovalRequested",
            new
            {
                workflowId,
                stepIndex,
                stepName,
                timestamp = DateTimeOffset.UtcNow
            },
            cancellationToken);
}
