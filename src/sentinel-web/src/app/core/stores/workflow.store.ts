import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ApprovalRequestedEvent,
  ProblemDetails,
  ScenarioDefinition,
  WorkflowDetailDto,
  WorkflowStatus,
  WorkflowSummaryDto
} from '../models/workflow.models';
import { WorkflowApiService } from '../services/workflow-api.service';
import { WorkflowSignalRService } from '../services/workflow-signalr.service';

export type OperatorIdentity = 'rafael' | 'oncall-sre' | 'platform-admin';

@Injectable({ providedIn: 'root' })
export class WorkflowStore {
  private readonly api = inject(WorkflowApiService);
  private readonly signalR = inject(WorkflowSignalRService);

  // State signals
  readonly workflows = signal<WorkflowSummaryDto[]>([]);
  readonly scenarios = signal<ScenarioDefinition[]>([]);
  readonly selectedWorkflow = signal<WorkflowDetailDto | null>(null);
  readonly selectedOperator = signal<OperatorIdentity>('rafael');
  readonly statusFilter = signal<WorkflowStatus | 'ALL'>('ALL');
  readonly loading = signal<boolean>(false);
  readonly triggeringScenario = signal<string | null>(null);
  readonly submittingDecision = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly conflictError = signal<string | null>(null);
  readonly approvalBanner = signal<ApprovalRequestedEvent | null>(null);

  // Connection signal passthrough
  readonly connectionState = this.signalR.connectionState;

  // Computed selectors
  readonly activeWorkflows = computed(() =>
    this.workflows().filter(
      (w) =>
        w.status === 'Pending' ||
        w.status === 'Running' ||
        w.status === 'EvaluatingPolicy' ||
        w.status === 'ExecutingStep'
    )
  );

  readonly pendingApprovals = computed(() =>
    this.workflows().filter((w) => w.status === 'AwaitingApproval')
  );

  readonly pendingApprovalsCount = computed(() => this.pendingApprovals().length);

  readonly blockedWorkflows = computed(() =>
    this.workflows().filter((w) => w.status === 'BlockedByPolicy')
  );

  readonly completedWorkflows = computed(() =>
    this.workflows().filter((w) => w.status === 'Completed')
  );

  readonly filteredWorkflows = computed(() => {
    const filter = this.statusFilter();
    const list = this.workflows();
    return filter === 'ALL' ? list : list.filter((w) => w.status === filter);
  });

  async initialize(): Promise<void> {
    await Promise.all([this.refreshAll(), this.loadScenarios()]);

    await this.signalR.connect({
      onWorkflowUpdated: (evt) => {
        void this.handleWorkflowUpdated(evt.workflowId);
      },
      onApprovalRequested: (evt) => {
        this.approvalBanner.set(evt);
        void this.handleWorkflowUpdated(evt.workflowId);
      },
      onReconnected: () => {
        void this.refreshAll();
      }
    });
  }

  async refreshAll(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const items = await firstValueFrom(this.api.getWorkflows());
      this.workflows.set(items);

      const current = this.selectedWorkflow();
      if (current) {
        const updated = await firstValueFrom(this.api.getWorkflowById(current.id));
        this.selectedWorkflow.set(updated);
      }
    } catch (err) {
      this.errorMessage.set(this.extractErrorMessage(err, 'Failed to load workflows.'));
    } finally {
      this.loading.set(false);
    }
  }

  async loadScenarios(): Promise<void> {
    try {
      const list = await firstValueFrom(this.api.getScenarios());
      this.scenarios.set(list);
    } catch {
      // Fallback scenarios if API is temporarily unreachable
    }
  }

  async triggerScenario(scenarioKey: string): Promise<void> {
    this.triggeringScenario.set(scenarioKey);
    this.errorMessage.set(null);
    this.conflictError.set(null);

    try {
      const created = await firstValueFrom(this.api.triggerScenario(scenarioKey));
      this.selectedWorkflow.set(created);
      await this.refreshWorkflowListOnly();
    } catch (err) {
      this.errorMessage.set(this.extractErrorMessage(err, `Failed to trigger scenario '${scenarioKey}'.`));
    } finally {
      this.triggeringScenario.set(null);
    }
  }

  async selectWorkflow(id: string): Promise<void> {
    this.conflictError.set(null);
    this.errorMessage.set(null);
    try {
      const detail = await firstValueFrom(this.api.getWorkflowById(id));
      this.selectedWorkflow.set(detail);
      if (this.approvalBanner()?.workflowId === id) {
        this.approvalBanner.set(null);
      }
    } catch (err) {
      this.errorMessage.set(this.extractErrorMessage(err, 'Failed to load workflow details.'));
    }
  }

  closeInspector(): void {
    this.selectedWorkflow.set(null);
    this.conflictError.set(null);
  }

  dismissApprovalBanner(): void {
    this.approvalBanner.set(null);
  }

  setOperator(operator: OperatorIdentity): void {
    this.selectedOperator.set(operator);
  }

  setStatusFilter(filter: WorkflowStatus | 'ALL'): void {
    this.statusFilter.set(filter);
  }

  async approveWorkflow(id: string, comment: string): Promise<void> {
    await this.submitDecision(id, comment, 'approve');
  }

  async rejectWorkflow(id: string, comment: string): Promise<void> {
    await this.submitDecision(id, comment, 'reject');
  }

  private async submitDecision(
    id: string,
    comment: string,
    action: 'approve' | 'reject'
  ): Promise<void> {
    this.submittingDecision.set(true);
    this.conflictError.set(null);
    this.errorMessage.set(null);

    const request = {
      operatorName: this.selectedOperator(),
      comment: comment.trim()
    };

    try {
      const updated =
        action === 'approve'
          ? await firstValueFrom(this.api.approveWorkflow(id, request))
          : await firstValueFrom(this.api.rejectWorkflow(id, request));

      this.selectedWorkflow.set(updated);
      if (this.approvalBanner()?.workflowId === id) {
        this.approvalBanner.set(null);
      }
      await this.refreshWorkflowListOnly();
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 409) {
        const problem = err.error as ProblemDetails | undefined;
        this.conflictError.set(
          problem?.detail ??
            'Concurrency conflict (409): Another operator modified this workflow. State has been refreshed.'
        );
        await this.refreshAll();
      } else {
        this.errorMessage.set(
          this.extractErrorMessage(err, `Failed to ${action} workflow.`)
        );
      }
    } finally {
      this.submittingDecision.set(false);
    }
  }

  private async handleWorkflowUpdated(workflowId: string): Promise<void> {
    await this.refreshWorkflowListOnly();

    const current = this.selectedWorkflow();
    if (current && current.id === workflowId) {
      try {
        const refreshed = await firstValueFrom(this.api.getWorkflowById(workflowId));
        this.selectedWorkflow.set(refreshed);
      } catch {
        // Ignore transient refresh error
      }
    }
  }

  private async refreshWorkflowListOnly(): Promise<void> {
    try {
      const items = await firstValueFrom(this.api.getWorkflows());
      this.workflows.set(items);
    } catch {
      // Ignore transient refresh error
    }
  }

  private extractErrorMessage(err: unknown, fallback: string): string {
    if (err instanceof HttpErrorResponse) {
      const problem = err.error as ProblemDetails | undefined;
      if (problem?.detail) {
        return problem.detail;
      }
      if (problem?.title) {
        return problem.title;
      }
    }
    return fallback;
  }
}
