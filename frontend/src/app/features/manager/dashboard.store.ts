import { Injectable, computed, inject } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import type { Schemas } from '../../core/api/api';
import { ManagerApi } from './manager.api';

export const DASHBOARD_POLL_MS = 30_000;

/**
 * Manager dashboard: exposure and workload reports. Provide it on the dashboard page, so polling stops when the
 * manager navigates away. reload() keeps the current figures on screen while it refreshes.
 */
@Injectable()
export class DashboardStore {
  private readonly api = inject(ManagerApi);
  private readonly exposure = rxResource({ stream: () => this.api.exposure() });
  private readonly workload = rxResource({ stream: () => this.api.workload() });

  /** True only for the first load, not for background polling. */
  readonly loading = computed(
    () => this.exposure.status() === 'loading' || this.workload.status() === 'loading',
  );
  readonly failed = computed(
    () => this.exposure.status() === 'error' || this.workload.status() === 'error',
  );
  /** hasValue() guards value(), which throws while the resource is in error. */
  readonly exposureLines = computed<Schemas['ExposureLine'][]>(() =>
    this.exposure.hasValue() ? (this.exposure.value()?.lines ?? []) : [],
  );
  readonly officers = computed<Schemas['OfficerWorkload'][]>(() =>
    this.workload.hasValue() ? (this.workload.value()?.officers ?? []) : [],
  );
  readonly asOf = computed(() =>
    this.exposure.hasValue() ? (this.exposure.value()?.asOf ?? null) : null,
  );

  constructor() {
    interval(DASHBOARD_POLL_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        this.exposure.reload();
        this.workload.reload();
      });
  }
}
