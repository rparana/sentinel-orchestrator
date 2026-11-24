import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { WorkflowDetailDto, WorkflowSummaryDto } from '../models/workflow.models';
import { WorkflowApiService } from '../services/workflow-api.service';
import { WorkflowSignalRService } from '../services/workflow-signalr.service';
import { WorkflowStore } from './workflow.store';

describe('WorkflowStore', () => {
  const sampleWorkflows: WorkflowSummaryDto[] = [
    {
      id: 'wf-1',
      name: 'Auto Triage',
      scenarioKey: 'auto-triage',
      status: 'Completed',
      currentStepIndex: 1,
      totalSteps: 2,
      rowVersion: 1,
      createdAt: '2026-11-24T12:00:00Z',
      updatedAt: '2026-11-24T12:00:02Z',
      completedAt: '2026-11-24T12:00:02Z'
    },
    {
      id: 'wf-2',
      name: 'Prod Scale',
      scenarioKey: 'prod-scale',
      status: 'AwaitingApproval',
      currentStepIndex: 1,
      totalSteps: 2,
      rowVersion: 2,
      createdAt: '2026-11-24T12:01:00Z',
      updatedAt: '2026-11-24T12:01:01Z',
      completedAt: null
    },
    {
      id: 'wf-3',
      name: 'Blocked Delete',
      scenarioKey: 'blocked-delete',
      status: 'BlockedByPolicy',
      currentStepIndex: 0,
      totalSteps: 1,
      rowVersion: 1,
      createdAt: '2026-11-24T12:02:00Z',
      updatedAt: '2026-11-24T12:02:01Z',
      completedAt: '2026-11-24T12:02:01Z'
    }
  ];

  const apiMock = {
    getWorkflows: vi.fn(() => of(sampleWorkflows)),
    getScenarios: vi.fn(() => of([])),
    getWorkflowById: vi.fn((id: string) =>
      of({
        ...sampleWorkflows[1],
        id,
        steps: [],
        auditEntries: []
      } as WorkflowDetailDto)
    ),
    triggerScenario: vi.fn(),
    approveWorkflow: vi.fn(),
    rejectWorkflow: vi.fn()
  };

  const signalRMock = {
    connectionState: signal<'connected'>('connected'),
    connect: vi.fn(async () => undefined),
    disconnect: vi.fn(async () => undefined)
  };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        WorkflowStore,
        { provide: WorkflowApiService, useValue: apiMock },
        { provide: WorkflowSignalRService, useValue: signalRMock }
      ]
    });
  });

  it('computes pendingApprovals, blockedWorkflows, and completedWorkflows from signal state', async () => {
    const store = TestBed.inject(WorkflowStore);
    await store.initialize();

    expect(store.pendingApprovalsCount()).toBe(1);
    expect(store.pendingApprovals()[0].id).toBe('wf-2');
    expect(store.blockedWorkflows().length).toBe(1);
    expect(store.completedWorkflows().length).toBe(1);
  });

  it('handles 409 Conflict gracefully on concurrent approval and refreshes state', async () => {
    const store = TestBed.inject(WorkflowStore);
    await store.initialize();

    apiMock.approveWorkflow.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { detail: 'Workflow already approved by another operator.' }
          })
      )
    );

    await store.approveWorkflow('wf-2', 'Concurrent attempt');

    expect(store.conflictError()).toContain('Workflow already approved');
    expect(apiMock.getWorkflows).toHaveBeenCalledTimes(2);
  });
});
