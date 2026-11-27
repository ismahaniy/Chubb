import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable, firstValueFrom } from 'rxjs';
import type { Schemas } from '../../core/api/api';
import { reloadOnNotification } from '../../core/notifications/notification.store';
import { OfficerApi } from './officer.api';

type ClaimDetail = Schemas['ClaimDetail'];
export type WorkbenchAction =
  'assign' | 'requestInformation' | 'assessment' | 'decision' | 'settle';

/**
 * One claim on the officer workbench. Provide it on the workbench page.
 *
 * Actions are offered by status only; the server owns the other rules (assigned officer only,
 * reserve before decision, maximum payable) and answers 403/409, which the error interceptor
 * toasts. After any failed action the claim is reloaded, so the screen never shows stale state.
 */
@Injectable()
export class WorkbenchStore {
  private readonly api = inject(OfficerApi);

  private readonly claimId = signal<string | undefined>(undefined);
  private readonly busyAction = signal<WorkbenchAction | null>(null);
  /** Undefined id = idle resource, no request. */
  private readonly claim = rxResource({
    params: () => this.claimId(),
    stream: ({ params }) => this.api.get(params),
  });

  readonly busy = this.busyAction.asReadonly();
  readonly loading = computed(() => this.claim.status() === 'loading');
  readonly failed = computed(() => this.claim.status() === 'error');
  /** hasValue() guards value(), which throws while the resource is in error. */
  readonly detail = computed<ClaimDetail | null>(() =>
    this.claim.hasValue() ? (this.claim.value() ?? null) : null,
  );
  readonly status = computed(() => this.detail()?.status ?? null);
  readonly canAssign = computed(() => this.status() === 'Submitted');
  /** Request information, record an assessment and decide all need UnderReview. */
  readonly canWork = computed(() => this.status() === 'UnderReview');
  readonly canSettle = computed(() => this.status() === 'Approved');

  constructor() {
    // E.g. the claimant answered an information request: show it without a manual refresh.
    reloadOnNotification(this.claimId, () => this.claim.reload());
  }

  select(id: string): void {
    this.claimId.set(id);
  }

  assign(): Promise<boolean> {
    return this.run('assign', (id) => this.api.assign(id));
  }

  requestInformation(question: string): Promise<boolean> {
    return this.run('requestInformation', (id) => this.api.requestInformation(id, { question }));
  }

  /** The reserve is always in the claim currency; the domain rejects any other. */
  recordAssessment(reserveAmount: number, notes: string): Promise<boolean> {
    return this.run('assessment', (id, claim) =>
      this.api.recordAssessment(id, {
        reserveAmount,
        currency: claim.claimedAmount.currency,
        notes: notes.trim() || null,
      }),
    );
  }

  decide(body: Schemas['DecisionRequest']): Promise<boolean> {
    return this.run('decision', (id) => this.api.decide(id, body));
  }

  settle(): Promise<boolean> {
    return this.run('settle', (id) => this.api.settle(id));
  }

  private async run(
    action: WorkbenchAction,
    call: (id: string, claim: ClaimDetail) => Observable<ClaimDetail>,
  ): Promise<boolean> {
    const claim = this.detail();
    if (claim === null || this.busyAction() !== null) return false;

    this.busyAction.set(action);
    try {
      this.claim.set(await firstValueFrom(call(claim.id, claim)));
      return true;
    } catch {
      this.claim.reload();
      return false;
    } finally {
      this.busyAction.set(null);
    }
  }
}
