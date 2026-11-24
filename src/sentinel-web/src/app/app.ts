import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { WorkflowStatus } from './core/models/workflow.models';
import { WorkflowSignalRService } from './core/services/workflow-signalr.service';
import { OperatorIdentity, WorkflowStore } from './core/stores/workflow.store';
import { ScenarioTriggerBarComponent } from './features/control-plane/components/scenario-trigger-bar.component';
import { StatusBadgeComponent } from './features/control-plane/components/status-badge.component';
import { WorkflowInspectorDrawerComponent } from './features/control-plane/components/workflow-inspector-drawer.component';

@Component({
  selector: 'app-root',
  imports: [
    DatePipe,
    FormsModule,
    ScenarioTriggerBarComponent,
    StatusBadgeComponent,
    WorkflowInspectorDrawerComponent
  ],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class App implements OnInit, OnDestroy {
  readonly store = inject(WorkflowStore);
  private readonly signalR = inject(WorkflowSignalRService);

  readonly operators: OperatorIdentity[] = ['rafael', 'oncall-sre', 'platform-admin'];
  readonly filterTabs: { label: string; value: WorkflowStatus | 'ALL' }[] = [
    { label: 'All', value: 'ALL' },
    { label: 'Awaiting Approval', value: 'AwaitingApproval' },
    { label: 'Running', value: 'Running' },
    { label: 'Completed', value: 'Completed' },
    { label: 'Blocked', value: 'BlockedByPolicy' },
    { label: 'Rejected', value: 'Rejected' }
  ];

  ngOnInit(): void {
    void this.store.initialize();
  }

  ngOnDestroy(): void {
    void this.signalR.disconnect();
  }
}
