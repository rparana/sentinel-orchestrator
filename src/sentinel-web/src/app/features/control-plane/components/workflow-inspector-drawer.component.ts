import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { WorkflowDetailDto } from '../../../core/models/workflow.models';
import { StatusBadgeComponent } from './status-badge.component';

@Component({
  selector: 'app-workflow-inspector-drawer',
  imports: [DatePipe, FormsModule, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (workflow(); as wf) {
      <div
        class="fixed inset-0 z-40 flex justify-end bg-slate-950/75 backdrop-blur-xs"
        role="dialog"
        aria-modal="true"
        aria-label="Workflow Execution Inspector"
      >
        <div class="fixed inset-0" (click)="close.emit()"></div>

        <aside class="relative z-50 flex h-full w-full max-w-2xl flex-col border-l border-slate-800 bg-slate-950 shadow-2xl">
          <!-- Header -->
          <header class="flex items-start justify-between gap-4 border-b border-slate-800 bg-slate-900/80 px-6 py-4">
            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2.5">
                <h2 class="truncate text-base font-semibold text-slate-100">{{ wf.name }}</h2>
                <app-status-badge [value]="wf.status" />
              </div>
              <div class="mt-1 flex flex-wrap items-center gap-3 font-mono text-xs text-slate-400">
                <span>ID: {{ wf.id }}</span>
                <span>xmin: {{ wf.rowVersion }}</span>
                @if (wf.scenarioKey) {
                  <span class="text-sky-400">scenario: {{ wf.scenarioKey }}</span>
                }
              </div>
            </div>

            <button
              type="button"
              (click)="close.emit()"
              class="rounded-lg border border-slate-700 bg-slate-900 px-2.5 py-1.5 text-xs font-medium text-slate-300 hover:border-slate-600 hover:text-white"
            >
              Close (Esc)
            </button>
          </header>

          <!-- Scrollable Content -->
          <div class="flex-1 space-y-6 overflow-y-auto px-6 py-5">
            <!-- 409 Conflict Banner -->
            @if (conflictError()) {
              <div class="rounded-lg border border-rose-500/40 bg-rose-500/10 p-3.5 text-xs text-rose-200">
                <div class="font-semibold text-rose-300">Optimistic Concurrency Conflict (409)</div>
                <p class="mt-1">{{ conflictError() }}</p>
              </div>
            }

            <!-- HITL Approval Panel -->
            @if (wf.status === 'AwaitingApproval') {
              <section class="rounded-xl border border-amber-500/40 bg-amber-500/10 p-4">
                <div class="flex items-center justify-between gap-2">
                  <div>
                    <h3 class="text-sm font-semibold text-amber-200">
                      Human-in-the-Loop Approval Required
                    </h3>
                    <p class="mt-0.5 text-xs text-amber-200/80">
                      Acting as operator <span class="font-mono font-semibold">{{ operatorName() }}</span>. Provide a mandatory audit justification.
                    </p>
                  </div>
                </div>

                <div class="mt-3">
                  <label for="audit-comment" class="block text-xs font-medium text-amber-100">
                    Audit Justification
                  </label>
                  <textarea
                    id="audit-comment"
                    rows="2"
                    [ngModel]="comment()"
                    (ngModelChange)="comment.set($event)"
                    placeholder="e.g., Approved during maintenance window after verifying replica capacity"
                    class="mt-1.5 w-full rounded-lg border border-amber-500/30 bg-slate-950/90 px-3 py-2 text-xs text-slate-100 placeholder:text-slate-500 focus:border-amber-400 focus:outline-none"
                  ></textarea>
                </div>

                <div class="mt-3 flex flex-wrap items-center gap-2.5">
                  <button
                    type="button"
                    [disabled]="submitting() || !comment().trim()"
                    (click)="onApprove(wf.id)"
                    class="rounded-lg bg-emerald-600 px-3.5 py-2 text-xs font-semibold text-white transition hover:bg-emerald-500 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {{ submitting() ? 'Submitting...' : 'Approve & Execute' }}
                  </button>
                  <button
                    type="button"
                    [disabled]="submitting() || !comment().trim()"
                    (click)="onReject(wf.id)"
                    class="rounded-lg border border-rose-500/40 bg-rose-500/15 px-3.5 py-2 text-xs font-semibold text-rose-200 transition hover:bg-rose-500/25 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Reject Workflow
                  </button>
                </div>
              </section>
            }

            <!-- Step-by-Step Execution Timeline -->
            <section>
              <h3 class="mb-3 text-xs font-semibold uppercase tracking-wider text-slate-400">
                Execution Steps ({{ wf.steps.length }})
              </h3>

              <div class="space-y-3">
                @for (step of wf.steps; track step.id) {
                  <div class="rounded-xl border border-slate-800 bg-slate-900/60 p-4">
                    <div class="flex flex-wrap items-center justify-between gap-2">
                      <div class="flex items-center gap-2">
                        <span class="flex h-5 w-5 items-center justify-center rounded-full bg-slate-800 font-mono text-xs text-slate-300">
                          {{ step.order + 1 }}
                        </span>
                        <span class="text-sm font-medium text-slate-100">{{ step.name }}</span>
                        <span class="rounded bg-slate-800 px-1.5 py-0.5 font-mono text-[11px] text-slate-400">
                          {{ step.type }}
                        </span>
                      </div>

                      <div class="flex items-center gap-2">
                        @if (step.durationMs !== null) {
                          <span class="font-mono text-xs text-slate-400">{{ step.durationMs }}ms</span>
                        }
                        <app-status-badge [value]="step.status" />
                      </div>
                    </div>

                    @if (step.policyDecision; as decision) {
                      <div class="mt-3 rounded-lg border border-slate-800 bg-slate-950/70 p-3">
                        <div class="flex flex-wrap items-center justify-between gap-2">
                          <span class="text-xs font-medium text-slate-300">
                            Policy Rule: <span class="font-mono text-sky-400">{{ decision.ruleName }}</span>
                          </span>
                          <app-status-badge [value]="decision.verdict" />
                        </div>
                        <p class="mt-1 text-xs text-slate-400">{{ decision.reason }}</p>
                      </div>
                    }

                    @if (step.proposedAction; as proposal) {
                      <div class="mt-3">
                        <div class="mb-1 flex items-center justify-between text-xs text-slate-400">
                          <span>
                            Proposed Action: <span class="font-mono text-slate-200">{{ proposal.actionName }}</span>
                            ({{ proposal.targetEnvironment }})
                          </span>
                          <app-status-badge [value]="proposal.riskTier" />
                        </div>
                        @if (proposal.rationale) {
                          <p class="mb-1.5 text-xs italic text-slate-400">{{ proposal.rationale }}</p>
                        }
                        <pre class="overflow-x-auto rounded-lg border border-slate-800 bg-slate-950 p-2.5 font-mono text-[11px] text-sky-200">{{ formatJson(proposal.payloadJson) }}</pre>
                      </div>
                    }

                    @if (step.outputJson) {
                      <div class="mt-3">
                        <div class="mb-1 text-xs font-medium text-emerald-400">Execution Output</div>
                        <pre class="overflow-x-auto rounded-lg border border-emerald-500/20 bg-slate-950 p-2.5 font-mono text-[11px] text-emerald-200">{{ formatJson(step.outputJson) }}</pre>
                      </div>
                    }

                    @if (step.errorMessage) {
                      <div class="mt-3 rounded-lg border border-rose-500/30 bg-rose-500/10 p-2.5 text-xs text-rose-200">
                        {{ step.errorMessage }}
                      </div>
                    }
                  </div>
                }
              </div>
            </section>

            <!-- Chronological Audit Log -->
            <section>
              <h3 class="mb-3 text-xs font-semibold uppercase tracking-wider text-slate-400">
                Audit Trail ({{ wf.auditEntries.length }})
              </h3>

              <div class="space-y-2 border-l border-slate-800 pl-4">
                @for (entry of wf.auditEntries; track entry.id) {
                  <div class="relative rounded-lg border border-slate-800/80 bg-slate-900/40 p-3">
                    <span class="absolute -left-[21px] top-4 h-2 w-2 rounded-full bg-slate-600"></span>
                    <div class="flex flex-wrap items-center justify-between gap-2 text-xs">
                      <div class="flex items-center gap-2">
                        <span class="font-mono font-semibold text-slate-200">{{ entry.eventType }}</span>
                        <span class="text-slate-500">&bull;</span>
                        <span class="font-mono text-sky-400">{{ entry.actor }}</span>
                      </div>
                      <span class="font-mono text-[11px] text-slate-500">
                        {{ entry.timestamp | date: 'HH:mm:ss.SSS' }}
                      </span>
                    </div>
                    <div class="mt-1 font-mono text-[11px] text-slate-400">
                      {{ entry.fromStatus }} &rarr; {{ entry.toStatus }}
                    </div>
                    <p class="mt-1 text-xs text-slate-300">{{ entry.details }}</p>
                  </div>
                }
              </div>
            </section>
          </div>
        </aside>
      </div>
    }
  `
})
export class WorkflowInspectorDrawerComponent {
  readonly workflow = input<WorkflowDetailDto | null>(null);
  readonly operatorName = input.required<string>();
  readonly submitting = input<boolean>(false);
  readonly conflictError = input<string | null>(null);

  readonly close = output<void>();
  readonly approve = output<{ id: string; comment: string }>();
  readonly reject = output<{ id: string; comment: string }>();

  readonly comment = signal('');

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.workflow()) {
      this.close.emit();
    }
  }

  onApprove(id: string): void {
    const trimmed = this.comment().trim();
    if (!trimmed) {
      return;
    }
    this.approve.emit({ id, comment: trimmed });
    this.comment.set('');
  }

  onReject(id: string): void {
    const trimmed = this.comment().trim();
    if (!trimmed) {
      return;
    }
    this.reject.emit({ id, comment: trimmed });
    this.comment.set('');
  }

  formatJson(raw: string): string {
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }
}
