import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import type { Schemas } from '../../../core/api/api';
import { ToastStore } from '../../../core/errors/toast.store';
import { SubmitClaimStore } from '../submit-claim.store';
import { ClaimForm } from '../ui/claim-form';

@Component({
  selector: 'app-submit-claim-page',
  imports: [RouterLink, ClaimForm],
  providers: [SubmitClaimStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/claimant" class="text-sm text-neutral-500 hover:text-neutral-900"
      >← My claims</a
    >
    <h2 class="mt-2 mb-6 text-lg font-semibold">Report a claim</h2>

    <div class="max-w-2xl rounded-lg border border-neutral-200 bg-white p-5">
      @if (store.policiesLoading()) {
        <p class="text-sm text-neutral-500">Loading your policies…</p>
      } @else if (store.policiesFailed()) {
        <p class="text-sm text-neutral-700">Your policies could not be loaded.</p>
      } @else if (store.policies().length === 0) {
        <p class="text-sm text-neutral-700">You have no policies to claim against.</p>
      } @else {
        <app-claim-form
          [policies]="store.policies()"
          [busy]="store.submitting()"
          (submitClaim)="submit($event)"
        />
      }
    </div>
  `,
})
export class SubmitClaimPage {
  protected readonly store = inject(SubmitClaimStore);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastStore);

  /** On failure (already toasted) the form keeps what the claimant typed. */
  protected async submit(request: Schemas['SubmitClaimRequest']): Promise<void> {
    const claim = await this.store.submit(request);
    if (claim === null) return;
    this.toasts.show({ kind: 'info', title: `Claim ${claim.claimNumber} submitted` });
    await this.router.navigate(['/claimant/claims', claim.id]);
  }
}
