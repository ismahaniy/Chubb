import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { Schemas } from '../../core/api/api';
import { Money } from './money';

/** A recorded decision: outcome, approved amount (approvals only), reason and time. */
@Component({
  selector: 'app-decision-summary',
  imports: [DatePipe, Money],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-lg border border-neutral-200 bg-white p-5 text-sm">
      <h3 class="mb-2 font-semibold">Decision: {{ decision().outcome }}</h3>
      @if (decision().approvedAmount; as amount) {
        <p>Approved amount: <app-money [value]="amount" /></p>
      }
      <p class="mt-1 whitespace-pre-line text-neutral-700">{{ decision().reason }}</p>
      <p class="mt-1 text-xs text-neutral-500">{{ decision().decidedAt | date: 'medium' }}</p>
    </section>
  `,
})
export class DecisionSummary {
  readonly decision = input.required<Schemas['DecisionDto']>();
}
