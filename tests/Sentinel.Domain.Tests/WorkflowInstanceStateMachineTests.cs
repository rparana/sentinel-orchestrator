using Sentinel.Domain.Entities;
using Sentinel.Domain.Enums;
using Sentinel.Domain.Exceptions;
using Sentinel.Domain.Policies;
using Sentinel.Domain.ValueObjects;
using Xunit;

namespace Sentinel.Domain.Tests;

public sealed class WorkflowInstanceStateMachineTests
{
    private readonly DeterministicPolicyEngine _policyEngine = new();

    [Fact]
    public void Create_WithValidInput_InitializesPendingStateAndAuditEntry()
    {
        var workflow = WorkflowInstance.Create(
            "Incident Triage",
            [new WorkflowStepDefinition("Analyze Logs", StepType.Diagnostic)],
            scenarioKey: "auto-triage",
            actor: "api");

        Assert.NotEqual(Guid.Empty, workflow.Id);
        Assert.Equal("Incident Triage", workflow.Name);
        Assert.Equal("auto-triage", workflow.ScenarioKey);
        Assert.Equal(WorkflowStatus.Pending, workflow.Status);
        Assert.Equal(0, workflow.CurrentStepIndex);
        Assert.Single(workflow.Steps);
        Assert.Single(workflow.AuditEntries);
        Assert.Equal("WorkflowCreated", workflow.AuditEntries[0].EventType);
        Assert.Equal("api", workflow.AuditEntries[0].Actor);
    }

    [Fact]
    public void Create_WithEmptyNameOrNoSteps_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            WorkflowInstance.Create(" ", [new WorkflowStepDefinition("Step", StepType.Diagnostic)]));

        Assert.Throws<DomainException>(() =>
            WorkflowInstance.Create("Valid", []));

        Assert.Throws<DomainException>(() =>
            WorkflowInstance.Create("Valid", [new WorkflowStepDefinition("", StepType.Diagnostic)]));
    }

    [Fact]
    public void MultiStepAutoAllowedWorkflow_CompletesEndToEndWithFullAuditTrail()
    {
        var workflow = WorkflowInstance.Create(
            "Auto Remediation",
            [
                new WorkflowStepDefinition("Gather Metrics", StepType.Diagnostic, RiskTier.Low, "production"),
                new WorkflowStepDefinition("Generate Report", StepType.ActionExecutor, RiskTier.Low, "staging")
            ]);

        workflow.Start();
        Assert.Equal(WorkflowStatus.Running, workflow.Status);
        Assert.Equal(StepStatus.Running, workflow.CurrentStep!.Status);

        var decision1 = workflow.SubmitProposalAndEvaluatePolicy(
            new ActionProposal("ReadMetrics", "production", RiskTier.Low),
            _policyEngine);

        Assert.Equal(PolicyVerdict.Allow, decision1.Verdict);
        Assert.Equal(WorkflowStatus.ExecutingStep, workflow.Status);

        workflow.CompleteCurrentStep("{\"latencyMs\":42}");
        Assert.Equal(WorkflowStatus.Running, workflow.Status);
        Assert.Equal(1, workflow.CurrentStepIndex);
        Assert.Equal(StepStatus.Completed, workflow.Steps[0].Status);
        Assert.Equal(StepStatus.Running, workflow.Steps[1].Status);

        var decision2 = workflow.SubmitProposalAndEvaluatePolicy(
            new ActionProposal("PublishReport", "staging", RiskTier.Medium),
            _policyEngine);

        Assert.Equal(PolicyVerdict.Allow, decision2.Verdict);
        workflow.CompleteCurrentStep("{\"reportId\":\"rep-1\"}");

        Assert.Equal(WorkflowStatus.Completed, workflow.Status);
        Assert.NotNull(workflow.CompletedAt);
        Assert.All(workflow.Steps, s => Assert.Equal(StepStatus.Completed, s.Status));
        Assert.Equal(8, workflow.AuditEntries.Count);
        Assert.Equal("WorkflowCompleted", workflow.AuditEntries[^1].EventType);
    }

    [Fact]
    public void HighRiskProductionWorkflow_PausesForApprovalAndCompletesAfterApprove()
    {
        var workflow = WorkflowInstance.Create(
            "Scale Prod DB",
            [new WorkflowStepDefinition("Scale Cluster", StepType.ActionExecutor, RiskTier.High, "production")]);

        workflow.Start();
        var decision = workflow.SubmitProposalAndEvaluatePolicy(
            new ActionProposal("ScaleDatabaseCluster", "production", RiskTier.High),
            _policyEngine);

        Assert.Equal(PolicyVerdict.RequireApproval, decision.Verdict);
        Assert.Equal(WorkflowStatus.AwaitingApproval, workflow.Status);
        Assert.Equal(StepStatus.AwaitingApproval, workflow.CurrentStep!.Status);

        workflow.Approve("rafael", "Approved during maintenance window");

        Assert.Equal(WorkflowStatus.ExecutingStep, workflow.Status);
        Assert.Equal(StepStatus.Executing, workflow.CurrentStep.Status);

        workflow.CompleteCurrentStep("{\"replicas\":4}");

        Assert.Equal(WorkflowStatus.Completed, workflow.Status);
        Assert.Contains(workflow.AuditEntries, e =>
            e.EventType == "ApprovalGranted" &&
            e.Actor == "operator:rafael" &&
            e.FromStatus == WorkflowStatus.AwaitingApproval &&
            e.ToStatus == WorkflowStatus.ExecutingStep);
    }

    [Fact]
    public void HighRiskProductionWorkflow_TransitionsToRejectedOnOperatorReject()
    {
        var workflow = WorkflowInstance.Create(
            "Rollback Prod Deploy",
            [new WorkflowStepDefinition("Rollback", StepType.ActionExecutor, RiskTier.High, "production")]);

        workflow.Start();
        workflow.SubmitProposalAndEvaluatePolicy(
            new ActionProposal("RollbackProductionDeploy", "production", RiskTier.High),
            _policyEngine);

        workflow.Reject("rafael", "False positive alert");

        Assert.Equal(WorkflowStatus.Rejected, workflow.Status);
        Assert.NotNull(workflow.CompletedAt);
        Assert.Equal(StepStatus.Rejected, workflow.CurrentStep!.Status);
        Assert.Equal("False positive alert", workflow.CurrentStep.ErrorMessage);
    }

    [Fact]
    public void DestructiveProposal_TransitionsToBlockedByPolicy()
    {
        var workflow = WorkflowInstance.Create(
            "Cleanup Storage",
            [new WorkflowStepDefinition("Delete Volume", StepType.ActionExecutor, RiskTier.Critical, "production")]);

        workflow.Start();
        var decision = workflow.SubmitProposalAndEvaluatePolicy(
            new ActionProposal("DeleteProductionVolume", "production", RiskTier.Critical),
            _policyEngine);

        Assert.Equal(PolicyVerdict.Deny, decision.Verdict);
        Assert.Equal(WorkflowStatus.BlockedByPolicy, workflow.Status);
        Assert.NotNull(workflow.CompletedAt);
        Assert.Equal(StepStatus.BlockedByPolicy, workflow.CurrentStep!.Status);
    }

    [Fact]
    public void FailCurrentStep_FromExecutingOrRunning_TransitionsToFailed()
    {
        var workflow = WorkflowInstance.Create(
            "Failing Job",
            [new WorkflowStepDefinition("Execute", StepType.ActionExecutor)]);

        workflow.Start();
        workflow.SubmitProposalAndEvaluatePolicy(
            new ActionProposal("RestartPod", "staging", RiskTier.Low),
            _policyEngine);

        workflow.FailCurrentStep("Timeout connecting to cluster");

        Assert.Equal(WorkflowStatus.Failed, workflow.Status);
        Assert.NotNull(workflow.CompletedAt);
        Assert.Equal(StepStatus.Failed, workflow.CurrentStep!.Status);
        Assert.Equal("Timeout connecting to cluster", workflow.CurrentStep.ErrorMessage);
    }

    [Fact]
    public void InvalidTransitions_ThrowInvalidWorkflowTransitionException()
    {
        var workflow = WorkflowInstance.Create(
            "Guard Test",
            [new WorkflowStepDefinition("Step 1", StepType.Diagnostic)]);

        Assert.Throws<InvalidWorkflowTransitionException>(() =>
            workflow.SubmitProposalAndEvaluatePolicy(
                new ActionProposal("Ping", "staging", RiskTier.Low),
                _policyEngine));
        Assert.Throws<InvalidWorkflowTransitionException>(() => workflow.Approve("op", "ok"));
        Assert.Throws<InvalidWorkflowTransitionException>(() => workflow.Reject("op", "no"));
        Assert.Throws<InvalidWorkflowTransitionException>(() => workflow.CompleteCurrentStep());
        Assert.Throws<InvalidWorkflowTransitionException>(() => workflow.FailCurrentStep("err"));

        workflow.Start();
        Assert.Throws<InvalidWorkflowTransitionException>(() => workflow.Start());
    }

    [Fact]
    public void ApproveOrReject_WithoutOperatorOrComment_ThrowsDomainException()
    {
        var workflow = WorkflowInstance.Create(
            "Approval Validation",
            [new WorkflowStepDefinition("Step 1", StepType.ActionExecutor)]);

        workflow.Start();
        workflow.SubmitProposalAndEvaluatePolicy(
            new ActionProposal("ScaleCluster", "production", RiskTier.High),
            _policyEngine);

        Assert.Throws<DomainException>(() => workflow.Approve("", "valid comment"));
        Assert.Throws<DomainException>(() => workflow.Approve("rafael", " "));
        Assert.Throws<DomainException>(() => workflow.Reject("", "valid comment"));
        Assert.Throws<DomainException>(() => workflow.Reject("rafael", ""));
    }
}
