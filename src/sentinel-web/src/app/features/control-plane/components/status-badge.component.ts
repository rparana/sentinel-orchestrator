import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PolicyVerdict, RiskTier, StepStatus, WorkflowStatus } from '../../../core/models/workflow.models';

@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span [class]="badgeClasses()">
      <span [class]="dotClasses()"></span>
      {{ label() }}
    </span>
  `
})
export class StatusBadgeComponent {
  readonly value = input.required<WorkflowStatus | StepStatus | PolicyVerdict | RiskTier | string>();

  readonly label = computed(() => this.value());

  readonly badgeClasses = computed(() => {
    const base =
      'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium tracking-tight';

    switch (this.value()) {
      case 'Completed':
      case 'Allow':
      case 'Low':
        return `${base} border-emerald-500/30 bg-emerald-500/10 text-emerald-300`;
      case 'AwaitingApproval':
      case 'RequireApproval':
      case 'High':
        return `${base} border-amber-500/40 bg-amber-500/15 text-amber-300`;
      case 'BlockedByPolicy':
      case 'Deny':
      case 'Failed':
      case 'Rejected':
      case 'Critical':
        return `${base} border-rose-500/30 bg-rose-500/10 text-rose-300`;
      case 'Running':
      case 'EvaluatingPolicy':
      case 'ExecutingStep':
      case 'Executing':
      case 'Medium':
        return `${base} border-sky-500/30 bg-sky-500/10 text-sky-300`;
      default:
        return `${base} border-slate-700 bg-slate-800/70 text-slate-300`;
    }
  });

  readonly dotClasses = computed(() => {
    const base = 'h-1.5 w-1.5 rounded-full';
    switch (this.value()) {
      case 'Completed':
      case 'Allow':
      case 'Low':
        return `${base} bg-emerald-400`;
      case 'AwaitingApproval':
      case 'RequireApproval':
      case 'High':
        return `${base} bg-amber-400 animate-pulse`;
      case 'BlockedByPolicy':
      case 'Deny':
      case 'Failed':
      case 'Rejected':
      case 'Critical':
        return `${base} bg-rose-400`;
      case 'Running':
      case 'EvaluatingPolicy':
      case 'ExecutingStep':
      case 'Executing':
      case 'Medium':
        return `${base} bg-sky-400 animate-pulse`;
      default:
        return `${base} bg-slate-400`;
    }
  });
}
