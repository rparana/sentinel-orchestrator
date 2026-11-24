namespace Sentinel.Application.Exceptions;

public sealed class WorkflowNotFoundException : Exception
{
    public Guid WorkflowId { get; }

    public WorkflowNotFoundException(Guid workflowId)
        : base($"Workflow '{workflowId}' was not found.")
    {
        WorkflowId = workflowId;
    }
}

public sealed class ScenarioNotFoundException : Exception
{
    public string ScenarioKey { get; }

    public ScenarioNotFoundException(string scenarioKey)
        : base($"Scenario '{scenarioKey}' is not registered.")
    {
        ScenarioKey = scenarioKey;
    }
}
