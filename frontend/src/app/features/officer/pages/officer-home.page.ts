import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Placeholder until the officer feature slice. */
@Component({
  selector: 'app-officer-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="text-lg font-semibold">Work queue</h2>
    <p class="mt-2 text-sm text-neutral-500">Claims waiting for review will appear here.</p>
  `,
})
export class OfficerHomePage {}
