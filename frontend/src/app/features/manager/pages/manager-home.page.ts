import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Placeholder until the manager feature slice. */
@Component({
  selector: 'app-manager-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="text-lg font-semibold">Overview</h2>
    <p class="mt-2 text-sm text-neutral-500">Workload and exposure reports will appear here.</p>
  `,
})
export class ManagerHomePage {}
