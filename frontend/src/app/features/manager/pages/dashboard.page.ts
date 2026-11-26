import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DASHBOARD_POLL_MS, DashboardStore } from '../dashboard.store';
import { ExposureTable } from '../ui/exposure-table';
import { WorkloadTable } from '../ui/workload-table';

@Component({
  selector: 'app-dashboard-page',
  imports: [DatePipe, ExposureTable, WorkloadTable],
  providers: [DashboardStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mb-6">
      <h2 class="text-lg font-semibold">Dashboard</h2>
      <p class="text-xs text-neutral-500">
        @if (store.asOf(); as asOf) {
          As of {{ asOf | date: 'medium' }} ·
        }
        refreshes every {{ pollSeconds }} s
      </p>
    </div>

    @if (store.failed()) {
      <p
        class="mb-4 rounded-md border border-neutral-300 bg-white px-3 py-2 text-sm text-neutral-700"
      >
        Could not load the reports. Retrying automatically.
      </p>
    }

    <div class="space-y-8" [class.opacity-50]="store.loading()">
      <section>
        <h3 class="mb-1 text-sm font-semibold">Liability exposure</h3>
        <p class="mb-3 text-xs text-neutral-500">
          Open claims (not rejected or settled) at their reserve, or the claimed amount until a
          reserve is set. Each currency is shown separately and never added together.
        </p>
        <app-exposure-table [lines]="store.exposureLines()" />
      </section>

      <section>
        <h3 class="mb-3 text-sm font-semibold">Workload per officer</h3>
        <app-workload-table [officers]="store.officers()" />
      </section>
    </div>
  `,
})
export class DashboardPage {
  protected readonly store = inject(DashboardStore);
  protected readonly pollSeconds = DASHBOARD_POLL_MS / 1000;
}
