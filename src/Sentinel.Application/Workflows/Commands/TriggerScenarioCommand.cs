using Sentinel.Application.Abstractions;
using Sentinel.Application.Exceptions;
using Sentinel.Application.Workflows.Dtos;
using Sentinel.Domain.Entities;

namespace Sentinel.Application.Workflows.Commands;

public sealed record TriggerScenarioCommand(
    string ScenarioKey,
    string Actor = "api");

public sealed class TriggerScenarioHandler
{
    private readonly IWorkflowRepository _repository;
    private readonly IWorkflowNotificationPublisher _notifier;

    public TriggerScenarioHandler(
        IWorkflowRepository repository,
        IWorkflowNotificationPublisher notifier)
    {
        _repository = repository;
        _notifier = notifier;
    }

    public async Task<WorkflowDetailDto> HandleAsync(
        TriggerScenarioCommand command,
        CancellationToken cancellationToken = default)
    {
        var scenario = ScenarioCatalog.FindByKey(command.ScenarioKey)
            ?? throw new ScenarioNotFoundException(command.ScenarioKey);

        var workflow = WorkflowInstance.Create(
            scenario.Title,
            scenario.Steps,
            scenario.Key,
            command.Actor);

        workflow.Start(command.Actor);

        await _repository.AddAsync(workflow, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _notifier.PublishWorkflowUpdatedAsync(workflow.Id, workflow.Status, cancellationToken);

        return workflow.ToDetailDto();
    }
}
