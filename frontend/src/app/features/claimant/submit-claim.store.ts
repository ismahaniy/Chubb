import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import type { Schemas } from '../../core/api/api';
import { ClaimantApi } from './claimant.api';

/**
 * Reporting a claim. Provide it on the page. The policy list lets the form take product type and currency from the
 * policy, so the claimant cannot pick a combination the domain rejects. Every other rule (holder, active policy,
 * incident date in the market's local calendar) stays with the server, which answers 404/409 (toasted).
 */
@Injectable()
export class SubmitClaimStore {
  private readonly api = inject(ClaimantApi);
  private readonly policyList = rxResource({ stream: () => this.api.policies() });
  private readonly busy = signal(false);

  readonly submitting = this.busy.asReadonly();
  readonly policiesLoading = computed(() => this.policyList.status() === 'loading');
  readonly policiesFailed = computed(() => this.policyList.status() === 'error');
  readonly policies = computed<Schemas['PolicySummary'][]>(() =>
    this.policyList.hasValue() ? (this.policyList.value() ?? []) : [],
  );

  /** The created claim, or null when the server refused it. */
  async submit(request: Schemas['SubmitClaimRequest']): Promise<Schemas['ClaimDetail'] | null> {
    if (this.busy()) return null;
    this.busy.set(true);
    try {
      return await firstValueFrom(this.api.submit(request));
    } catch {
      return null;
    } finally {
      this.busy.set(false);
    }
  }
}
