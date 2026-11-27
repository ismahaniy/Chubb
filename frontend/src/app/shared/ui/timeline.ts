import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { Schemas } from '../../core/api/api';
import { STATUS_DISPLAY } from './status-badge';

/** Status history, newest first. */
@Component({
  selector: 'app-timeline',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol class="relative space-y-4 border-l border-neutral-200 pl-4">
      @for (entry of newestFirst(); track $index) {
        <li class="relative">
          <span
            class="absolute top-1.5 -left-[21px] h-2.5 w-2.5 rounded-full border-2 border-white bg-neutral-400"
          ></span>
          <p class="text-sm text-neutral-900">
            @if (entry.fromStatus) {
              {{ label(entry.fromStatus) }} <span class="text-neutral-400">→</span>
            }
            <span class="font-medium">{{ label(entry.toStatus) }}</span>
          </p>
          <p class="text-xs text-neutral-500">{{ entry.changedAt | date: 'medium' }}</p>
        </li>
      } @empty {
        <li class="text-sm text-neutral-500">No status changes yet.</li>
      }
    </ol>
  `,
})
export class Timeline {
  readonly entries = input.required<readonly Schemas['StatusChangeDto'][]>();

  protected readonly newestFirst = computed(() =>
    [...this.entries()].sort((a, b) => Date.parse(b.changedAt) - Date.parse(a.changedAt)),
  );

  protected label(status: Schemas['ClaimStatus']): string {
    return STATUS_DISPLAY[status].label;
  }
}
