import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import type { Schemas } from '../../core/api/api';
import { reloadOnNotification } from '../../core/notifications/notification.store';
import { ClaimantApi } from './claimant.api';

type ClaimDetail = Schemas['ClaimDetail'];

/**
 * One of the claimant's claims. Provide it on the claim page. The server enforces ownership (403) and the answer rules
 * (409); after a failed answer the claim is reloaded so the screen never shows stale state.
 */
@Injectable()
export class ClaimStore {
  private readonly api = inject(ClaimantApi);

  private readonly claimId = signal<string | undefined>(undefined);
  private readonly respondingTo = signal<string | null>(null);
  /** Undefined id = idle resource, no request. */
  private readonly claim = rxResource({
    params: () => this.claimId(),
    stream: ({ params }) => this.api.get(params),
  });

  /** The information request being answered, if any. */
  readonly responding = this.respondingTo.asReadonly();
  readonly loading = computed(() => this.claim.status() === 'loading');
  readonly failed = computed(() => this.claim.status() === 'error');
  /** hasValue() guards value(), which throws while the resource is in error. */
  readonly detail = computed<ClaimDetail | null>(() =>
    this.claim.hasValue() ? (this.claim.value() ?? null) : null,
  );

  constructor() {
    // An officer's action (picked up, info requested, decided, settled) notifies the claimant: show it at once.
    reloadOnNotification(this.claimId, () => this.claim.reload());
  }

  select(id: string): void {
    this.claimId.set(id);
  }

  async respond(requestId: string, answer: string): Promise<boolean> {
    const claim = this.detail();
    if (claim === null || this.respondingTo() !== null) return false;

    this.respondingTo.set(requestId);
    try {
      this.claim.set(await firstValueFrom(this.api.respond(claim.id, requestId, { answer })));
      return true;
    } catch {
      this.claim.reload();
      return false;
    } finally {
      this.respondingTo.set(null);
    }
  }
}
