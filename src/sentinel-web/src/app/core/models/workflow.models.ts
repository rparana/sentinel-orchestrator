export type WorkflowStatus =
  | 'Pending'
  | 'Running'
  | 'EvaluatingPolicy'
  | 'AwaitingApproval'
  | 'BlockedByPolicy'
  | 'ExecutingStep'
  | 'Completed'
  | 'Failed'
  | 'Rejected';

export type StepType = 'Diagnostic' | 'AiPlanner' | 'ActionExecutor';

export type StepStatus =
  | 'Pending'
  | 'Running'
  | 'EvaluatingPolicy'
  | 'AwaitingApproval'
  | 'BlockedByPolicy'
  | 'Executing'
  | 'Completed'
  | 'Failed'
  | 'Rejected';

export type PolicyVerdict = 'Allow' | 'RequireApproval' | 'Deny';

export type RiskTier = 'Low' | 'Medium' | 'High' | 'Critical';

export interface ActionProposal {
  actionName: string;
  targetEnvironment: string;
  riskTier: RiskTier;
  payloadJson: string;
  rationale: string;
}

export interface PolicyDecision {
  verdict: PolicyVerdict;
  ruleName: string;
  reason: string;
  evaluatedAt: string;
}

export interface WorkflowStepDto {
  id: string;
  order: number;
  name: string;
  type: StepType;
  status: StepStatus;
  defaultRiskTier: RiskTier;
  targetEnvironment: string;
  proposedAction: ActionProposal | null;
  policyDecision: PolicyDecision | null;
  outputJson: string | null;
  errorMessage: string | null;
  startedAt: string | null;
  completedAt: string | null;
  durationMs: number | null;
}

export interface WorkflowAuditEntryDto {
  id: string;
  timestamp: string;
  actor: string;
  eventType: string;
  fromStatus: WorkflowStatus;
  toStatus: WorkflowStatus;
  details: string;
}

export interface WorkflowSummaryDto {
  id: string;
  name: string;
  scenarioKey: string | null;
  status: WorkflowStatus;
  currentStepIndex: number;
  totalSteps: number;
  rowVersion: number;
  createdAt: string;
  updatedAt: string | null;
  completedAt: string | null;
}

export interface WorkflowDetailDto {
  id: string;
  name: string;
  scenarioKey: string | null;
  status: WorkflowStatus;
  currentStepIndex: number;
  rowVersion: number;
  createdAt: string;
  updatedAt: string | null;
  completedAt: string | null;
  steps: WorkflowStepDto[];
  auditEntries: WorkflowAuditEntryDto[];
}

export interface ScenarioDefinition {
  key: string;
  title: string;
  description: string;
  expectedRisk: RiskTier;
}

export interface WorkflowUpdatedEvent {
  workflowId: string;
  status: WorkflowStatus;
  timestamp: string;
}

export interface ApprovalRequestedEvent {
  workflowId: string;
  stepIndex: number;
  stepName: string;
  timestamp: string;
}

export interface OperatorDecisionRequest {
  operatorName: string;
  comment: string;
}

export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
}
