namespace Sentinel.Domain.Enums;

public enum StepStatus
{
    Pending = 0,
    Running = 1,
    EvaluatingPolicy = 2,
    AwaitingApproval = 3,
    BlockedByPolicy = 4,
    Executing = 5,
    Completed = 6,
    Failed = 7,
    Rejected = 8
}
