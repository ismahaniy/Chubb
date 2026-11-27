import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom, interval } from 'rxjs';
import type { Schemas } from '../../core/api/api';
import { OfficerApi, QueueQuery } from './officer.api';

export const QUEUE_POLL_MS = 10_000;
const PAGE_SIZE = 20;

/**
 * Officer work queue. Provide it on the queue page: its lifetime is the page's, so polling stops
 * when the officer navigates away.
 *
 * The page is an rxResource keyed on the query signal: a filter change cancels any request still
 * in flight, so a slow, stale response can never overwrite a newer one. Polling uses reload(),
 * which keeps the current rows on screen while it refreshes (and is a no-op mid-load).
 */
@Injectable()
export class QueueStore {
  private readonly api = inject(OfficerApi);

  private readonly state = signal<QueueQuery>({
    scope: 'Unassigned',
    page: 1,
    pageSize: PAGE_SIZE,
  });
  private readonly assigning = signal<string | null>(null);
  private readonly result = rxResource({
    params: () => this.state(),
    stream: ({ params }) => this.api.queue(params),
  });

  readonly query = this.state.asReadonly();
  readonly assigningId = this.assigning.asReadonly();
  /** True only for a fresh load (first load or a filter change), not for background polling. */
  readonly loading = computed(() => this.result.status() === 'loading');
  readonly failed = computed(() => this.result.status() === 'error');
  /** hasValue() guards value(), which throws while the resource is in error. */
  readonly items = computed<Schemas['QueueItem'][]>(() =>
    this.result.hasValue() ? (this.result.value()?.items ?? []) : [],
  );
  readonly totalCount = computed(() =>
    this.result.hasValue() ? Number(this.result.value()?.totalCount ?? 0) : 0,
  );
  readonly page = computed(() => Number(this.state().page ?? 1));
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / PAGE_SIZE)));

  constructor() {
    interval(QUEUE_POLL_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.result.reload());
  }

  /** Switching tabs clears the status filter: it only applies to "Mine" (unassigned claims are all Submitted). */
  setScope(scope: Schemas['QueueScope']): void {
    this.state.update((q) => ({ ...q, scope, status: undefined, page: 1 }));
  }

  setFilters(filters: Pick<QueueQuery, 'market' | 'status'>): void {
    this.state.update((q) => ({ ...q, ...filters, page: 1 }));
  }

  setPage(page: number): void {
    this.state.update((q) => ({ ...q, page }));
  }

  /**
   * Picks up a claim. Always reloads afterwards: on success the claim leaves "Unassigned",
   * on a lost race (409, already toasted) the stale row disappears.
   */
  async assign(id: string): Promise<boolean> {
    this.assigning.set(id);
    try {
      await firstValueFrom(this.api.assign(id));
      return true;
    } catch {
      return false;
    } finally {
      this.assigning.set(null);
      this.result.reload();
    }
  }
}
