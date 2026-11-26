import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ToastStore } from '../../../core/errors/toast.store';
import { ClaimFacts } from '../../../shared/ui/claim-facts';
import { DecisionSummary } from '../../../shared/ui/decision-summary';
import { Money } from '../../../shared/ui/money';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { Timeline } from '../../../shared/ui/timeline';
import { DecisionPanel } from '../ui/decision-panel';
import { WorkbenchStore } from '../workbench.store';

const INPUT_CLASSES =
  'mt-1 block w-full rounded-md border border-neutral-300 px-3 py-2 focus:border-neutral-500 focus:outline-none';

@Component({
  selector: 'app-workbench-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    ClaimFacts,
    DecisionSummary,
    Money,
    StatusBadge,
    Timeline,
    DecisionPanel,
  ],
  providers: [WorkbenchStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/officer" class="text-sm text-neutral-500 hover:text-neutral-900"
      >← Work queue</a
    >

    @if (store.detail(); as claim) {
      <div class="mt-2 mb-6 flex flex-wrap items-center gap-3">
        <h2 class="text-lg font-semibold">{{ claim.claimNumber }}</h2>
        <app-status-badge [status]="claim.status" />
      </div>

      <div class="grid gap-6 lg:grid-cols-3">
        <div class="space-y-6 lg:col-span-2">
          <app-claim-facts [claim]="claim" />

          @if (claim.decision; as decision) {
            <app-decision-summary [decision]="decision" />
          }

          @if (claim.openInformationRequests.length > 0) {
            <section class="rounded-lg border border-neutral-200 bg-white p-5 text-sm">
              <h3 class="mb-2 font-semibold">Waiting for the claimant</h3>
              <ul class="space-y-2">
                @for (request of claim.openInformationRequests; track request.id) {
                  <li>
                    <p>{{ request.question }}</p>
                    <p class="text-xs text-neutral-500">
                      Asked {{ request.requestedAt | date: 'medium' }}
                    </p>
                  </li>
                }
              </ul>
            </section>
          }

          @if (store.canWork()) {
            <section class="rounded-lg border border-neutral-200 bg-white p-5">
              <h3 class="mb-3 text-sm font-semibold">Assessment</h3>
              <form class="space-y-3" [formGroup]="assessmentForm" (ngSubmit)="recordAssessment()">
                <label class="block text-sm">
                  <span class="text-neutral-700">Reserve ({{ claim.claimedAmount.currency }})</span>
                  <input
                    type="number"
                    min="0.01"
                    step="0.01"
                    formControlName="reserveAmount"
                    [class]="inputClasses"
                  />
                </label>
                <label class="block text-sm">
                  <span class="text-neutral-700">Notes</span>
                  <textarea
                    rows="3"
                    maxlength="2000"
                    formControlName="notes"
                    [class]="inputClasses"
                  ></textarea>
                </label>
                <button
                  type="submit"
                  [class]="secondaryButton"
                  [disabled]="assessmentForm.invalid || store.busy() !== null"
                >
                  {{ store.busy() === 'assessment' ? 'Saving…' : 'Save assessment' }}
                </button>
              </form>
            </section>

            <section class="rounded-lg border border-neutral-200 bg-white p-5">
              <h3 class="mb-3 text-sm font-semibold">Request information</h3>
              <form class="space-y-3" [formGroup]="questionForm" (ngSubmit)="requestInformation()">
                <label class="block text-sm">
                  <span class="text-neutral-700">Question for the claimant</span>
                  <textarea
                    rows="3"
                    maxlength="2000"
                    formControlName="question"
                    [class]="inputClasses"
                  ></textarea>
                </label>
                <button
                  type="submit"
                  [class]="secondaryButton"
                  [disabled]="questionForm.invalid || store.busy() !== null"
                >
                  {{ store.busy() === 'requestInformation' ? 'Sending…' : 'Send request' }}
                </button>
              </form>
            </section>

            <section class="rounded-lg border border-neutral-200 bg-white p-5">
              <h3 class="mb-1 text-sm font-semibold">Decision</h3>
              <p class="mb-3 text-xs text-neutral-500">
                Record an assessment (reserve) before deciding.
              </p>
              <app-decision-panel
                [maxPayable]="claim.maxPayable"
                [busy]="store.busy() !== null"
                (decide)="store.decide($event)"
              />
            </section>
          }

          @if (store.canAssign()) {
            <section class="rounded-lg border border-neutral-200 bg-white p-5">
              <p class="mb-3 text-sm text-neutral-700">Nobody has picked up this claim yet.</p>
              <button
                type="button"
                [class]="primaryButton"
                [disabled]="store.busy() !== null"
                (click)="store.assign()"
              >
                {{ store.busy() === 'assign' ? 'Assigning…' : 'Assign to me' }}
              </button>
            </section>
          }

          @if (store.canSettle()) {
            <section class="rounded-lg border border-neutral-200 bg-white p-5">
              <h3 class="mb-3 text-sm font-semibold">Settlement</h3>
              @if (!confirmingSettle()) {
                <button type="button" [class]="primaryButton" (click)="confirmingSettle.set(true)">
                  Settle claim
                </button>
              } @else {
                <p class="mb-3 text-sm text-neutral-700">
                  Pay <app-money [value]="claim.decision?.approvedAmount ?? claim.maxPayable" /> and
                  close the claim?
                </p>
                <div class="flex gap-2">
                  <button
                    type="button"
                    [class]="primaryButton"
                    [disabled]="store.busy() !== null"
                    (click)="settle()"
                  >
                    {{ store.busy() === 'settle' ? 'Settling…' : 'Confirm settle' }}
                  </button>
                  <button
                    type="button"
                    [class]="secondaryButton"
                    (click)="confirmingSettle.set(false)"
                  >
                    Cancel
                  </button>
                </div>
              }
            </section>
          }
        </div>

        <aside class="rounded-lg border border-neutral-200 bg-white p-5">
          <h3 class="mb-3 text-sm font-semibold">Timeline</h3>
          <app-timeline [entries]="claim.timeline" />
        </aside>
      </div>
    } @else if (store.loading()) {
      <p class="mt-4 text-sm text-neutral-500">Loading claim…</p>
    } @else if (store.failed()) {
      <p class="mt-4 text-sm text-neutral-700">This claim could not be loaded.</p>
    }
  `,
})
export class WorkbenchPage {
  protected readonly store = inject(WorkbenchStore);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly toasts = inject(ToastStore);

  /** Bound from the :id route parameter (withComponentInputBinding). */
  readonly id = input.required<string>();

  protected readonly inputClasses = INPUT_CLASSES;
  protected readonly primaryButton =
    'rounded-md bg-neutral-900 px-4 py-2 text-sm font-medium text-white hover:bg-neutral-700 disabled:opacity-50';
  protected readonly secondaryButton =
    'rounded-md border border-neutral-300 bg-white px-4 py-2 text-sm font-medium hover:bg-neutral-100 disabled:opacity-50';
  protected readonly confirmingSettle = signal(false);

  protected readonly assessmentForm = this.fb.group({
    reserveAmount: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(0.01),
    ]),
    notes: ['', Validators.maxLength(2000)],
  });
  protected readonly questionForm = this.fb.group({
    question: ['', [Validators.required, Validators.maxLength(2000)]],
  });

  constructor() {
    // The router reuses this component when only :id changes, so forward every new id.
    effect(() => this.store.select(this.id()));
  }

  protected async recordAssessment(): Promise<void> {
    const { reserveAmount, notes } = this.assessmentForm.getRawValue();
    if (reserveAmount === null) return;
    // Keep the values on screen: the claim view does not return the reserve, so this is the
    // officer's only record of what was just saved.
    if (await this.store.recordAssessment(reserveAmount, notes)) {
      this.toasts.show({ kind: 'info', title: 'Assessment saved' });
      this.assessmentForm.markAsPristine();
    }
  }

  protected async requestInformation(): Promise<void> {
    const question = this.questionForm.getRawValue().question.trim();
    if (await this.store.requestInformation(question)) this.questionForm.reset();
  }

  protected async settle(): Promise<void> {
    await this.store.settle();
    this.confirmingSettle.set(false);
  }
}
