using Sentinel.Domain.Enums;

namespace Sentinel.Domain.Entities;

public sealed class WorkflowAuditEntry
{
    public Guid Id { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public WorkflowStatus FromStatus { get; private set; }
    public WorkflowStatus ToStatus { get; private set; }
    public string Details { get; private set; } = string.Empty;

    private WorkflowAuditEntry()
    {
    }

    internal WorkflowAuditEntry(
        Guid workflowInstanceId,
        string actor,
        string eventType,
        WorkflowStatus fromStatus,
        WorkflowStatus toStatus,
        string details,
        DateTimeOffset? timestamp = null)
    {
        Id = Guid.NewGuid();
        WorkflowInstanceId = workflowInstanceId;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
        Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
        EventType = eventType;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Details = details;
    }
}
