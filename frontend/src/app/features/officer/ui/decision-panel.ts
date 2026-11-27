import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import type { Schemas } from '../../../core/api/api';
import { Money } from '../../../shared/ui/money';

type Outcome = Schemas['DecisionOutcome'];

/**
 * An approval needs an amount above zero and within the maximum payable. This mirrors the domain
 * rule for fast feedback only; the server stays the authority.
 */
export function approvalAmountValidator(maxPayable: () => number): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    if (group.get('outcome')?.value !== 'Approved') return null;
    const amount = group.get('approvedAmount')?.value as number | null;
    if (amount === null || Number.isNaN(amount)) return { amountRequired: true };
    if (amount <= 0) return { amountNotPositive: true };
    if (amount > maxPayable()) return { amountOverMax: true };
    return null;
  };
}

/**
 * Approve or reject. Emits a request that matches the API's rules: a rejection carries no amount
 * or currency (the API answers 400 if it does).
 */
@Component({
  selector: 'app-decision-panel',
  imports: [ReactiveFormsModule, Money],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="space-y-3" [formGroup]="form" (ngSubmit)="submit()">
      <fieldset class="flex gap-4 text-sm">
        <legend class="sr-only">Outcome</legend>
        <label class="flex items-center gap-2">
          <input type="radio" formControlName="outcome" value="Approved" /> Approve
        </label>
        <label class="flex items-center gap-2">
          <input type="radio" formControlName="outcome" value="Rejected" /> Reject
        </label>
      </fieldset>

      @if (form.controls.outcome.value === 'Approved') {
        <label class="block text-sm">
          <span class="text-neutral-700">Approved amount ({{ maxPayable().currency }})</span>
          <input
            type="number"
            min="0.01"
            step="0.01"
            formControlName="approvedAmount"
            class="mt-1 block w-full rounded-md border border-neutral-300 px-3 py-2 focus:border-neutral-500 focus:outline-none"
          />
          <span class="mt-1 block text-xs text-neutral-500">
            Maximum payable: <app-money [value]="maxPayable()" />
          </span>
        </label>
        @if (form.hasError('amountOverMax')) {
          <p class="text-xs text-neutral-900">The amount is above the maximum payable.</p>
        } @else if (form.hasError('amountNotPositive')) {
          <p class="text-xs text-neutral-900">The amount must be greater than zero.</p>
        }
      }

      <label class="block text-sm">
        <span class="text-neutral-700">Reason</span>
        <textarea
          rows="3"
          maxlength="2000"
          formControlName="reason"
          class="mt-1 block w-full rounded-md border border-neutral-300 px-3 py-2 focus:border-neutral-500 focus:outline-none"
        ></textarea>
      </label>

      <button
        type="submit"
        class="rounded-md bg-neutral-900 px-4 py-2 text-sm font-medium text-white hover:bg-neutral-700 disabled:opacity-50"
        [disabled]="form.invalid || busy()"
      >
        {{ busy() ? 'Saving…' : 'Record decision' }}
      </button>
    </form>
  `,
})
export class DecisionPanel {
  readonly maxPayable = input.required<Schemas['MoneyDto']>();
  readonly busy = input(false);
  readonly decide = output<Schemas['DecisionRequest']>();

  protected readonly form = new FormGroup(
    {
      outcome: new FormControl<Outcome | null>(null, Validators.required),
      approvedAmount: new FormControl<number | null>(null),
      reason: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(2000)],
      }),
    },
    { validators: approvalAmountValidator(() => Number(this.maxPayable().amount)) },
  );

  protected submit(): void {
    if (this.form.invalid) return;
    const { outcome, approvedAmount, reason } = this.form.getRawValue();
    const approved = outcome === 'Approved';
    this.decide.emit({
      outcome,
      approvedAmount: approved ? approvedAmount : null,
      currency: approved ? this.maxPayable().currency : null,
      reason: reason.trim(),
    });
  }
}
