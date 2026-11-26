import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { Schemas } from '../../core/api/api';
import { MarketLabelPipe } from './markets';
import { Money } from './money';

/** The facts of a claim as the claimant reported them, plus what the policy can pay. Same for every role. */
@Component({
  selector: 'app-claim-facts',
  imports: [DatePipe, MarketLabelPipe, Money],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-lg border border-neutral-200 bg-white p-5">
      <h3 class="mb-3 text-sm font-semibold">Claim details</h3>
      <dl class="grid grid-cols-2 gap-x-6 gap-y-3 text-sm sm:grid-cols-3">
        <div>
          <dt class="text-neutral-500">Policy</dt>
          <dd>{{ claim().policyNumber }}</dd>
        </div>
        <div>
          <dt class="text-neutral-500">Market</dt>
          <dd>{{ claim().market | marketLabel }}</dd>
        </div>
        <div>
          <dt class="text-neutral-500">Product</dt>
          <dd>{{ claim().productType }}</dd>
        </div>
        <div>
          <dt class="text-neutral-500">Incident date</dt>
          <dd>{{ claim().incidentDate | date: 'mediumDate' }}</dd>
        </div>
        <div>
          <dt class="text-neutral-500">Submitted</dt>
          <dd>{{ claim().submittedAt | date: 'medium' }}</dd>
        </div>
        <div></div>
        <div>
          <dt class="text-neutral-500">Claimed</dt>
          <dd><app-money [value]="claim().claimedAmount" /></dd>
        </div>
        <div>
          <dt class="text-neutral-500">Maximum payable</dt>
          <dd><app-money [value]="claim().maxPayable" /></dd>
        </div>
      </dl>
      <p class="mt-4 text-sm whitespace-pre-line text-neutral-800">{{ claim().description }}</p>
    </section>
  `,
})
export class ClaimFacts {
  readonly claim = input.required<Schemas['ClaimDetail']>();
}
