import { Injectable, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { Schemas } from '../../core/api/api';
import { ANY_CLAIM, reloadOnNotification } from '../../core/notifications/notification.store';
import { ClaimantApi } from './claimant.api';

/** The claimant's claims. Provide it on the page. Refreshes when any notification arrives (a claim changed). */
@Injectable()
export class MyClaimsStore {
  private readonly api = inject(ClaimantApi);
  private readonly result = rxResource({ stream: () => this.api.mine() });

  readonly loading = computed(() => this.result.status() === 'loading');
  readonly failed = computed(() => this.result.status() === 'error');
  /** hasValue() guards value(), which throws while the resource is in error. */
  readonly items = computed<Schemas['ClaimSummary'][]>(() =>
    this.result.hasValue() ? (this.result.value() ?? []) : [],
  );

  constructor() {
    reloadOnNotification(
      () => ANY_CLAIM,
      () => this.result.reload(),
    );
  }
}
