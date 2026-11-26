import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { Schemas } from '../../core/api/api';

type ClaimStatus = Schemas['ClaimStatus'];

/** Label and classes per status. `Record<ClaimStatus, …>` makes the compiler flag a status added to the API. */
export const STATUS_DISPLAY: Record<ClaimStatus, { label: string; classes: string }> = {
  Submitted: { label: 'Submitted', classes: 'bg-white text-neutral-700 ring-neutral-300' },
  UnderReview: {
    label: 'Under review',
    classes: 'bg-neutral-100 text-neutral-800 ring-neutral-300',
  },
  AwaitingInformation: {
    label: 'Awaiting information',
    classes: 'bg-white text-neutral-900 ring-neutral-500',
  },
  Approved: { label: 'Approved', classes: 'bg-neutral-800 text-white ring-neutral-800' },
  Rejected: {
    label: 'Rejected',
    classes: 'bg-white text-neutral-500 ring-neutral-300 line-through',
  },
  Settled: { label: 'Settled', classes: 'bg-neutral-900 text-white ring-neutral-900' },
};

@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap ring-1"
      [class]="display().classes"
      >{{ display().label }}</span
    >
  `,
})
export class StatusBadge {
  readonly status = input.required<ClaimStatus>();

  protected readonly display = computed(() => STATUS_DISPLAY[this.status()]);
}
