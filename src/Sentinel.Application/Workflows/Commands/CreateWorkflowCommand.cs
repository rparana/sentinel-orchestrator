using Sentinel.Application.Abstractions;
using Sentinel.Application.Workflows.Dtos;
using Sentinel.Domain.Entities;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Application.Workflows.Commands;

public sealed record CreateWorkflowCommand(
    string Name,
    IReadOnlyList<WorkflowStepDefinition> Steps,
    string? ScenarioKey = null,
    string Actor = "api");

public sealed class CreateWorkflowHandler
{
    private readonly IWorkflowRepository _repository;
    private readonly IWorkflowNotificationPublisher _notifier;

    public CreateWorkflowHandler(
        IWorkflowRepository repository,
        IWorkflowNotificationPublisher notifier)
    {
        _repository = repository;
        _notifier = notifier;
    }

    public async Task<WorkflowDetailDto> HandleAsync(
        CreateWorkflowCommand command,
        CancellationToken cancellationToken = default)
    {
        var workflow = WorkflowInstance.Create(
            command.Name,
            command.Steps,
            command.ScenarioKey,
            command.Actor);

        workflow.Start(command.Actor);

        await _repository.AddAsync(workflow, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _notifier.PublishWorkflowUpdatedAsync(workflow.Id, workflow.Status, cancellationToken);

        return workflow.ToDetailDto();
    }
}
