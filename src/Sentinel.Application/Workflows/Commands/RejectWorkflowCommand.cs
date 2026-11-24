using Sentinel.Application.Abstractions;
using Sentinel.Application.Exceptions;
using Sentinel.Application.Workflows.Dtos;

namespace Sentinel.Application.Workflows.Commands;

public sealed record RejectWorkflowCommand(
    Guid WorkflowId,
    string OperatorName,
    string Comment);

public sealed class RejectWorkflowHandler
{
    private readonly IWorkflowRepository _repository;
    private readonly IWorkflowNotificationPublisher _notifier;

    public RejectWorkflowHandler(
        IWorkflowRepository repository,
        IWorkflowNotificationPublisher notifier)
    {
        _repository = repository;
        _notifier = notifier;
    }

    public async Task<WorkflowDetailDto> HandleAsync(
        RejectWorkflowCommand command,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _repository.GetByIdAsync(command.WorkflowId, cancellationToken)
            ?? throw new WorkflowNotFoundException(command.WorkflowId);

        workflow.Reject(command.OperatorName, command.Comment);
        await _repository.SaveChangesAsync(cancellationToken);

        await _notifier.PublishWorkflowUpdatedAsync(workflow.Id, workflow.Status, cancellationToken);

        return workflow.ToDetailDto();
    }
}
