using Sentinel.Domain.Enums;

namespace Sentinel.Domain.Exceptions;

public sealed class InvalidWorkflowTransitionException : DomainException
{
    public Guid WorkflowId { get; }
    public WorkflowStatus CurrentStatus { get; }
    public string AttemptedAction { get; }

    public InvalidWorkflowTransitionException(
        Guid workflowId,
        WorkflowStatus currentStatus,
        string attemptedAction)
        : base($"Cannot execute '{attemptedAction}' on workflow '{workflowId}' while in status '{currentStatus}'.")
    {
        WorkflowId = workflowId;
        CurrentStatus = currentStatus;
        AttemptedAction = attemptedAction;
    }
}
