import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { Toast } from '../../core/errors/toast.store';

@Component({
  selector: 'app-toast-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fixed right-4 bottom-4 z-50 flex w-80 flex-col gap-2" aria-live="polite">
      @for (toast of toasts(); track toast.id) {
        <div
          class="rounded-md border bg-white p-3 shadow-sm"
          [class]="toast.kind === 'error' ? 'border-red-300' : 'border-neutral-200'"
          role="status"
        >
          <div class="flex items-start justify-between gap-3">
            <p class="text-sm font-medium text-neutral-900">{{ toast.title }}</p>
            <button
              type="button"
              class="text-neutral-400 hover:text-neutral-700"
              aria-label="Dismiss"
              (click)="dismiss.emit(toast.id)"
            >
              &times;
            </button>
          </div>
          @if (toast.detail) {
            <p class="mt-1 text-sm text-neutral-600">{{ toast.detail }}</p>
          }
        </div>
      }
    </div>
  `,
})
export class ToastList {
  readonly toasts = input.required<readonly Toast[]>();
  readonly dismiss = output<number>();
}
