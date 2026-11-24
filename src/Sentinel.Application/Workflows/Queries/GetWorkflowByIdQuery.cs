using Sentinel.Application.Abstractions;
using Sentinel.Application.Exceptions;
using Sentinel.Application.Workflows.Dtos;

namespace Sentinel.Application.Workflows.Queries;

public sealed record GetWorkflowByIdQuery(Guid WorkflowId);

public sealed class GetWorkflowByIdHandler
{
    private readonly IWorkflowRepository _repository;

    public GetWorkflowByIdHandler(IWorkflowRepository repository)
    {
        _repository = repository;
    }

    public async Task<WorkflowDetailDto> HandleAsync(
        GetWorkflowByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _repository.GetByIdAsync(query.WorkflowId, cancellationToken)
            ?? throw new WorkflowNotFoundException(query.WorkflowId);

        return workflow.ToDetailDto();
    }
}
