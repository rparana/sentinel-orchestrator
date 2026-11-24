using Sentinel.Application.Abstractions;
using Sentinel.Application.Exceptions;
using Sentinel.Application.Workflows.Dtos;

namespace Sentinel.Application.Workflows.Commands;

public sealed record ApproveWorkflowCommand(
    Guid WorkflowId,
    string OperatorName,
    string Comment);

public sealed class ApproveWorkflowHandler
{
    private readonly IWorkflowRepository _repository;
    private readonly IWorkflowNotificationPublisher _notifier;

    public ApproveWorkflowHandler(
        IWorkflowRepository repository,
        IWorkflowNotificationPublisher notifier)
    {
        _repository = repository;
        _notifier = notifier;
    }

    public async Task<WorkflowDetailDto> HandleAsync(
        ApproveWorkflowCommand command,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _repository.GetByIdAsync(command.WorkflowId, cancellationToken)
            ?? throw new WorkflowNotFoundException(command.WorkflowId);

        workflow.Approve(command.OperatorName, command.Comment);
        await _repository.SaveChangesAsync(cancellationToken);

        await _notifier.PublishWorkflowUpdatedAsync(workflow.Id, workflow.Status, cancellationToken);

        return workflow.ToDetailDto();
    }
}
