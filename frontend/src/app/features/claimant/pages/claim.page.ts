import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ClaimFacts } from '../../../shared/ui/claim-facts';
import { DecisionSummary } from '../../../shared/ui/decision-summary';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { Timeline } from '../../../shared/ui/timeline';
import { ClaimStore } from '../claim.store';
import { InfoRequestResponse } from '../ui/info-request-response';

@Component({
  selector: 'app-claim-page',
  imports: [RouterLink, ClaimFacts, DecisionSummary, StatusBadge, Timeline, InfoRequestResponse],
  providers: [ClaimStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/claimant" class="text-sm text-neutral-500 hover:text-neutral-900"
      >← My claims</a
    >

    @if (store.detail(); as claim) {
      <div class="mt-2 mb-6 flex flex-wrap items-center gap-3">
        <h2 class="text-lg font-semibold">{{ claim.claimNumber }}</h2>
        <app-status-badge [status]="claim.status" />
      </div>

      <div class="grid gap-6 lg:grid-cols-3">
        <div class="space-y-6 lg:col-span-2">
          @for (request of claim.openInformationRequests; track request.id) {
            <app-info-request-response
              [request]="request"
              [busy]="store.responding() !== null"
              (answer)="store.respond(request.id, $event)"
            />
          }

          @if (claim.decision; as decision) {
            <app-decision-summary [decision]="decision" />
          }

          <app-claim-facts [claim]="claim" />
        </div>

        <aside class="rounded-lg border border-neutral-200 bg-white p-5">
          <h3 class="mb-3 text-sm font-semibold">Timeline</h3>
          <app-timeline [entries]="claim.timeline" />
        </aside>
      </div>
    } @else if (store.loading()) {
      <p class="mt-4 text-sm text-neutral-500">Loading claim…</p>
    } @else if (store.failed()) {
      <p class="mt-4 text-sm text-neutral-700">This claim could not be loaded.</p>
    }
  `,
})
export class ClaimPage {
  protected readonly store = inject(ClaimStore);

  /** Bound from the :id route parameter (withComponentInputBinding). */
  readonly id = input.required<string>();

  constructor() {
    // The router reuses this component when only :id changes, so forward every new id.
    effect(() => this.store.select(this.id()));
  }
}
