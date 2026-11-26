import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import type { Schemas } from '../../../core/api/api';
import { notBlank } from '../../../shared/forms/not-blank';
import { MARKET_LABELS } from '../../../shared/ui/markets';
import { Money } from '../../../shared/ui/money';

type PolicySummary = Schemas['PolicySummary'];

const INPUT_CLASSES =
  'mt-1 block w-full rounded-md border border-neutral-300 bg-white px-3 py-2 focus:border-neutral-500 focus:outline-none';

/**
 * Report a claim against one of the claimant's policies. Product type and currency come from the chosen policy and
 * are never typed, so they always match it. The incident date is only hinted to the policy period: whether it is "in
 * the future" depends on the market's local date, which the server decides.
 */
@Component({
  selector: 'app-claim-form',
  imports: [ReactiveFormsModule, Money],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="space-y-4" [formGroup]="form" (ngSubmit)="submit()">
      <label class="block text-sm">
        <span class="text-neutral-700">Policy</span>
        <select formControlName="policyNumber" [class]="inputClasses">
          <option value="" disabled>Choose a policy…</option>
          @for (policy of policies(); track policy.policyNumber) {
            <option [value]="policy.policyNumber">
              {{ policy.policyNumber }} · {{ marketLabels[policy.market] }} ·
              {{ policy.productType }}
            </option>
          }
        </select>
      </label>

      @if (selected(); as policy) {
        <p class="rounded-md bg-neutral-100 px-3 py-2 text-xs text-neutral-700">
          {{ policy.productType }} cover in {{ policy.currency }}, up to
          <app-money [value]="policy.maxPayable" /> per claim. In force {{ policy.startDate }} to
          {{ policy.endDate }}.
        </p>
      }

      <div class="grid gap-4 sm:grid-cols-2">
        <label class="block text-sm">
          <span class="text-neutral-700">Incident date</span>
          <input
            type="date"
            formControlName="incidentDate"
            [attr.min]="selected()?.startDate ?? null"
            [attr.max]="selected()?.endDate ?? null"
            [class]="inputClasses"
          />
        </label>
        <label class="block text-sm">
          <span class="text-neutral-700">Amount claimed {{ currencyHint() }}</span>
          <input
            type="number"
            min="0.01"
            step="0.01"
            formControlName="claimedAmount"
            [class]="inputClasses"
          />
        </label>
      </div>

      <label class="block text-sm">
        <span class="text-neutral-700">What happened?</span>
        <textarea
          rows="5"
          maxlength="2000"
          formControlName="description"
          [class]="inputClasses"
        ></textarea>
      </label>

      <button
        type="submit"
        class="rounded-md bg-neutral-900 px-4 py-2 text-sm font-medium text-white hover:bg-neutral-700 disabled:opacity-50"
        [disabled]="form.invalid || selected() === null || busy()"
      >
        {{ busy() ? 'Submitting…' : 'Submit claim' }}
      </button>
    </form>
  `,
})
export class ClaimForm {
  readonly policies = input.required<readonly PolicySummary[]>();
  readonly busy = input(false);
  readonly submitClaim = output<Schemas['SubmitClaimRequest']>();

  protected readonly inputClasses = INPUT_CLASSES;
  protected readonly marketLabels = MARKET_LABELS;

  protected readonly form = new FormGroup({
    policyNumber: new FormControl('', { nonNullable: true, validators: Validators.required }),
    incidentDate: new FormControl('', { nonNullable: true, validators: Validators.required }),
    claimedAmount: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(0.01),
    ]),
    description: new FormControl('', {
      nonNullable: true,
      validators: [notBlank, Validators.maxLength(2000)],
    }),
  });

  private readonly policyNumber = toSignal(this.form.controls.policyNumber.valueChanges, {
    initialValue: this.form.controls.policyNumber.value,
  });
  protected readonly selected = computed(
    () => this.policies().find((p) => p.policyNumber === this.policyNumber()) ?? null,
  );
  protected readonly currencyHint = computed(() => {
    const policy = this.selected();
    return policy ? `(${policy.currency})` : '';
  });

  protected submit(): void {
    const policy = this.selected();
    const { incidentDate, claimedAmount, description } = this.form.getRawValue();
    if (this.form.invalid || policy === null || claimedAmount === null) return;
    this.submitClaim.emit({
      policyNumber: policy.policyNumber,
      productType: policy.productType,
      currency: policy.currency,
      incidentDate,
      claimedAmount,
      description: description.trim(),
    });
  }
}
