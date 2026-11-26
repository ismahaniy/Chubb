import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import type { Schemas } from '../../../core/api/api';
import { STATUS_DISPLAY } from '../../../shared/ui/status-badge';
import { QUEUE_POLL_MS, QueueStore } from '../queue.store';
import { QueueTable } from '../ui/queue-table';

type Market = Schemas['Market'];
type ClaimStatus = Schemas['ClaimStatus'];

/** `Record<Market, …>` makes the compiler flag a market added to the API. */
const MARKET_LABELS: Record<Market, string> = {
  Singapore: 'Singapore',
  HongKong: 'Hong Kong',
  Malaysia: 'Malaysia',
  Thailand: 'Thailand',
  Indonesia: 'Indonesia',
  Philippines: 'Philippines',
};

/** The statuses the "Mine" scope can contain (see QueueScope.Mine on the API). */
const MINE_STATUSES: readonly ClaimStatus[] = ['UnderReview', 'AwaitingInformation', 'Approved'];

const TABS: readonly { scope: Schemas['QueueScope']; label: string }[] = [
  { scope: 'Unassigned', label: 'Unassigned' },
  { scope: 'Mine', label: 'My claims' },
];

@Component({
  selector: 'app-queue-page',
  imports: [QueueTable],
  providers: [QueueStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mb-4 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h2 class="text-lg font-semibold">Work queue</h2>
        <p class="text-xs text-neutral-500">
          Most urgent SLA first · refreshes every {{ pollSeconds }} s
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-3 text-sm">
        <label class="flex items-center gap-2">
          <span class="text-neutral-600">Market</span>
          <select
            class="rounded-md border border-neutral-300 bg-white px-2 py-1"
            [value]="store.query().market ?? ''"
            (change)="setMarket($any($event.target).value)"
          >
            <option value="">All</option>
            @for (market of markets; track market.value) {
              <option [value]="market.value">{{ market.label }}</option>
            }
          </select>
        </label>
        @if (store.query().scope === 'Mine') {
          <label class="flex items-center gap-2">
            <span class="text-neutral-600">Status</span>
            <select
              class="rounded-md border border-neutral-300 bg-white px-2 py-1"
              [value]="store.query().status ?? ''"
              (change)="setStatus($any($event.target).value)"
            >
              <option value="">All</option>
              @for (status of statuses; track status.value) {
                <option [value]="status.value">{{ status.label }}</option>
              }
            </select>
          </label>
        }
      </div>
    </div>

    <div class="mb-3 flex gap-1 border-b border-neutral-200" role="tablist">
      @for (tab of tabs; track tab.scope) {
        <button
          type="button"
          role="tab"
          class="-mb-px border-b-2 px-3 py-2 text-sm"
          [class]="
            store.query().scope === tab.scope
              ? 'border-neutral-900 font-medium text-neutral-900'
              : 'border-transparent text-neutral-500 hover:text-neutral-900'
          "
          [attr.aria-selected]="store.query().scope === tab.scope"
          (click)="store.setScope(tab.scope)"
        >
          {{ tab.label }}
        </button>
      }
    </div>

    @if (store.failed()) {
      <p
        class="mb-3 rounded-md border border-neutral-300 bg-white px-3 py-2 text-sm text-neutral-700"
      >
        Could not load the queue. Retrying automatically.
      </p>
    }

    <div [class.opacity-50]="store.loading()">
      <app-queue-table
        [items]="store.items()"
        [scope]="store.query().scope ?? 'Unassigned'"
        [assigningId]="store.assigningId()"
        (assign)="assign($event)"
        (open)="open($event)"
      />
    </div>

    <div class="mt-3 flex items-center justify-between text-sm text-neutral-600">
      <span>{{ store.totalCount() }} claim(s)</span>
      <div class="flex items-center gap-2">
        <button
          type="button"
          class="rounded-md border border-neutral-300 px-2 py-1 disabled:opacity-40"
          [disabled]="store.page() <= 1"
          (click)="store.setPage(store.page() - 1)"
        >
          Previous
        </button>
        <span>Page {{ store.page() }} of {{ store.totalPages() }}</span>
        <button
          type="button"
          class="rounded-md border border-neutral-300 px-2 py-1 disabled:opacity-40"
          [disabled]="store.page() >= store.totalPages()"
          (click)="store.setPage(store.page() + 1)"
        >
          Next
        </button>
      </div>
    </div>
  `,
})
export class QueuePage {
  protected readonly store = inject(QueueStore);
  private readonly router = inject(Router);

  protected readonly tabs = TABS;
  protected readonly pollSeconds = QUEUE_POLL_MS / 1000;
  protected readonly markets = Object.entries(MARKET_LABELS).map(([value, label]) => ({
    value,
    label,
  }));
  protected readonly statuses = MINE_STATUSES.map((value) => ({
    value,
    label: STATUS_DISPLAY[value].label,
  }));

  protected setMarket(value: string): void {
    this.store.setFilters({ market: (value || undefined) as Market | undefined });
  }

  protected setStatus(value: string): void {
    this.store.setFilters({ status: (value || undefined) as ClaimStatus | undefined });
  }

  protected async assign(id: string): Promise<void> {
    if (await this.store.assign(id)) await this.open(id);
  }

  protected async open(id: string): Promise<void> {
    await this.router.navigate(['/officer/claims', id]);
  }
}
