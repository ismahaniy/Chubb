import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import type { Schemas } from '../../../core/api/api';
import { notBlank } from '../../../shared/forms/not-blank';

/**
 * One open question from the officer, with the claimant's answer box. The answer stays in the box if sending fails;
 * on success the request leaves the claim's open list, so this component goes away.
 */
@Component({
  selector: 'app-info-request-response',
  imports: [DatePipe, ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-lg border border-neutral-400 bg-white p-5 text-sm">
      <h3 class="mb-1 font-semibold">We need more information</h3>
      <p class="whitespace-pre-line text-neutral-800">{{ request().question }}</p>
      <p class="mt-1 mb-3 text-xs text-neutral-500">
        Asked {{ request().requestedAt | date: 'medium' }}
      </p>
      <form class="space-y-3" (ngSubmit)="submit()">
        <label class="block">
          <span class="text-neutral-700">Your answer</span>
          <textarea
            rows="4"
            maxlength="2000"
            [formControl]="answerControl"
            class="mt-1 block w-full rounded-md border border-neutral-300 px-3 py-2 focus:border-neutral-500 focus:outline-none"
          ></textarea>
        </label>
        <button
          type="submit"
          class="rounded-md bg-neutral-900 px-4 py-2 text-sm font-medium text-white hover:bg-neutral-700 disabled:opacity-50"
          [disabled]="answerControl.invalid || busy()"
        >
          {{ busy() ? 'Sending…' : 'Send answer' }}
        </button>
      </form>
    </section>
  `,
})
export class InfoRequestResponse {
  readonly request = input.required<Schemas['InformationRequestDto']>();
  readonly busy = input(false);
  readonly answer = output<string>();

  protected readonly answerControl = new FormControl('', {
    nonNullable: true,
    validators: [notBlank, Validators.maxLength(2000)],
  });

  protected submit(): void {
    if (this.answerControl.invalid) return;
    this.answer.emit(this.answerControl.value.trim());
  }
}
