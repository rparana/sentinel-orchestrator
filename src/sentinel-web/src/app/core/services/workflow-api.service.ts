import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  OperatorDecisionRequest,
  ScenarioDefinition,
  WorkflowDetailDto,
  WorkflowStatus,
  WorkflowSummaryDto
} from '../models/workflow.models';

@Injectable({ providedIn: 'root' })
export class WorkflowApiService {
  private readonly http = inject(HttpClient);

  getWorkflows(status?: WorkflowStatus): Observable<WorkflowSummaryDto[]> {
    let params = new HttpParams();
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<WorkflowSummaryDto[]>('/api/workflows', { params });
  }

  getWorkflowById(id: string): Observable<WorkflowDetailDto> {
    return this.http.get<WorkflowDetailDto>(`/api/workflows/${id}`);
  }

  getScenarios(): Observable<ScenarioDefinition[]> {
    return this.http.get<ScenarioDefinition[]>('/api/scenarios');
  }

  triggerScenario(scenarioKey: string): Observable<WorkflowDetailDto> {
    return this.http.post<WorkflowDetailDto>(`/api/scenarios/${encodeURIComponent(scenarioKey)}/trigger`, {});
  }

  approveWorkflow(id: string, request: OperatorDecisionRequest): Observable<WorkflowDetailDto> {
    return this.http.post<WorkflowDetailDto>(`/api/workflows/${id}/approve`, request);
  }

  rejectWorkflow(id: string, request: OperatorDecisionRequest): Observable<WorkflowDetailDto> {
    return this.http.post<WorkflowDetailDto>(`/api/workflows/${id}/reject`, request);
  }
}
