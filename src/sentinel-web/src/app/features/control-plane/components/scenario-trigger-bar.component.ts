import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ScenarioDefinition } from '../../../core/models/workflow.models';
import { StatusBadgeComponent } from './status-badge.component';

@Component({
  selector: 'app-scenario-trigger-bar',
  imports: [StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-xl border border-slate-800 bg-slate-900/70 p-4">
      <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 class="text-sm font-semibold uppercase tracking-wider text-slate-200">
            Showcase Scenario Trigger Bar
          </h2>
          <p class="text-xs text-slate-400">
            Launch deterministic end-to-end operational workflows and observe real-time policy enforcement.
          </p>
        </div>
      </div>

      <div class="grid gap-3 md:grid-cols-3">
        @for (scenario of scenarios(); track scenario.key) {
          <button
            type="button"
            [disabled]="triggeringKey() !== null"
            (click)="trigger.emit(scenario.key)"
            class="group flex flex-col justify-between rounded-lg border border-slate-800 bg-slate-950/80 p-3.5 text-left transition hover:border-sky-500/50 hover:bg-slate-900 disabled:cursor-not-allowed disabled:opacity-60"
          >
            <div>
              <div class="mb-2 flex items-center justify-between gap-2">
                <span class="font-mono text-xs text-sky-400">{{ scenario.key }}</span>
                <app-status-badge [value]="scenario.expectedRisk" />
              </div>
              <div class="text-sm font-medium text-slate-100 group-hover:text-white">
                {{ scenario.title }}
              </div>
              <p class="mt-1 text-xs leading-relaxed text-slate-400">
                {{ scenario.description }}
              </p>
            </div>

            <div class="mt-3 flex items-center justify-between border-t border-slate-800/80 pt-2.5 text-xs font-medium text-sky-400">
              <span>
                @if (triggeringKey() === scenario.key) {
                  Triggering...
                } @else {
                  Trigger Scenario
                }
              </span>
              <span aria-hidden="true">&rarr;</span>
            </div>
          </button>
        }
      </div>
    </section>
  `
})
export class ScenarioTriggerBarComponent {
  readonly scenarios = input.required<ScenarioDefinition[]>();
  readonly triggeringKey = input<string | null>(null);
  readonly trigger = output<string>();
}
