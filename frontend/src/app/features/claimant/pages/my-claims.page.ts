import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MyClaimsStore } from '../my-claims.store';
import { ClaimList } from '../ui/claim-list';

@Component({
  selector: 'app-my-claims-page',
  imports: [RouterLink, ClaimList],
  providers: [MyClaimsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mb-4 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h2 class="text-lg font-semibold">My claims</h2>
        <p class="text-xs text-neutral-500">Newest first · updates when we notify you</p>
      </div>
      <a
        routerLink="new"
        class="rounded-md bg-neutral-900 px-4 py-2 text-sm font-medium text-white hover:bg-neutral-700"
        >Report a claim</a
      >
    </div>

    @if (store.failed()) {
      <p
        class="mb-3 rounded-md border border-neutral-300 bg-white px-3 py-2 text-sm text-neutral-700"
      >
        Could not load your claims.
      </p>
    }

    <div [class.opacity-50]="store.loading()">
      <app-claim-list [items]="store.items()" (open)="open($event)" />
    </div>
  `,
})
export class MyClaimsPage {
  protected readonly store = inject(MyClaimsStore);
  private readonly router = inject(Router);

  protected async open(id: string): Promise<void> {
    await this.router.navigate(['/claimant/claims', id]);
  }
}
