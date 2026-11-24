using Sentinel.Application.Abstractions;
using Sentinel.Application.Workflows.Dtos;
using Sentinel.Domain.Enums;

namespace Sentinel.Application.Workflows.Queries;

public sealed record GetWorkflowsQuery(WorkflowStatus? Status = null);

public sealed class GetWorkflowsHandler
{
    private readonly IWorkflowRepository _repository;

    public GetWorkflowsHandler(IWorkflowRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<WorkflowSummaryDto>> HandleAsync(
        GetWorkflowsQuery query,
        CancellationToken cancellationToken = default)
    {
        var workflows = await _repository.ListAsync(query.Status, cancellationToken);
        return workflows.Select(w => w.ToSummaryDto()).ToList();
    }
}
