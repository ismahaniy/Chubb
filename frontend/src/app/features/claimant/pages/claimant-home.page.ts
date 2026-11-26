import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Placeholder until the claimant feature slice. */
@Component({
  selector: 'app-claimant-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="text-lg font-semibold">My claims</h2>
    <p class="mt-2 text-sm text-neutral-500">Your submitted claims will appear here.</p>
  `,
})
export class ClaimantHomePage {}
