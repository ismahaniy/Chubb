import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import type { Schemas } from '../../core/api/api';

type NotificationView = Schemas['NotificationView'];

/**
 * Bell with an unread count and a dropdown list. Emits `closed` when the list is dismissed (that is when the shown
 * notifications count as seen) and `select` for a chosen notification (the list closes first).
 */
@Component({
  selector: 'app-notification-bell',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'close()' },
  template: `
    <div class="relative">
      <button
        type="button"
        class="relative rounded-md p-1.5 text-neutral-600 hover:bg-neutral-100 hover:text-neutral-900"
        [attr.aria-label]="label()"
        [attr.aria-expanded]="isOpen()"
        (click)="isOpen() ? close() : isOpen.set(true)"
      >
        <svg
          viewBox="0 0 24 24"
          class="h-5 w-5"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          aria-hidden="true"
        >
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            d="M14.857 17.082a23.848 23.848 0 0 0 5.454-1.31A8.967 8.967 0 0 1 18 9.75V9A6 6 0 0 0 6 9v.75a8.967 8.967 0 0 1-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 0 1-5.714 0m5.714 0a3 3 0 1 1-5.714 0"
          />
        </svg>
        @if (unreadCount() > 0) {
          <span
            class="absolute -top-0.5 -right-0.5 min-w-4 rounded-full bg-neutral-900 px-1 text-center text-[10px] leading-4 font-semibold text-white"
            >{{ unreadCount() > 9 ? '9+' : unreadCount() }}</span
          >
        }
      </button>

      @if (isOpen()) {
        <!-- Click-away layer: closes the list without a document listener. -->
        <button
          type="button"
          class="fixed inset-0 z-40 cursor-default"
          aria-label="Close notifications"
          (click)="close()"
        ></button>
        <div
          class="absolute right-0 z-50 mt-2 w-80 overflow-hidden rounded-lg border border-neutral-200 bg-white shadow-lg"
          role="dialog"
          aria-label="Notifications"
        >
          <p
            class="border-b border-neutral-100 px-4 py-2 text-xs font-medium tracking-wide text-neutral-500 uppercase"
          >
            Notifications
          </p>
          @if (failed()) {
            <p class="border-b border-neutral-100 px-4 py-2 text-xs text-neutral-600">
              Could not refresh. Retrying automatically.
            </p>
          }
          <ul class="max-h-96 divide-y divide-neutral-100 overflow-y-auto">
            @for (item of items(); track item.id) {
              <li>
                <button
                  type="button"
                  class="flex w-full gap-3 px-4 py-3 text-left hover:bg-neutral-50 disabled:cursor-default"
                  [disabled]="!selectable()"
                  (click)="choose(item)"
                >
                  <span
                    class="mt-1.5 h-2 w-2 shrink-0 rounded-full"
                    [class]="item.readAt === null ? 'bg-neutral-900' : 'bg-transparent'"
                    [attr.aria-label]="item.readAt === null ? 'Unread' : null"
                  ></span>
                  <span>
                    <span class="block text-sm text-neutral-900">{{ item.message }}</span>
                    <span class="block text-xs text-neutral-500">{{
                      item.createdAt | date: 'medium'
                    }}</span>
                  </span>
                </button>
              </li>
            } @empty {
              <li class="px-4 py-6 text-center text-sm text-neutral-500">No notifications yet.</li>
            }
          </ul>
        </div>
      }
    </div>
  `,
})
export class NotificationBell {
  readonly items = input.required<readonly NotificationView[]>();
  readonly unreadCount = input.required<number>();
  readonly failed = input(false);
  /** False when the user's role has no claim page to open (managers). */
  readonly selectable = input(true);
  readonly closed = output();
  readonly select = output<NotificationView>();

  protected readonly isOpen = signal(false);

  protected readonly label = computed(() => {
    const n = this.unreadCount();
    return n === 0 ? 'Notifications' : `Notifications, ${n} unread`;
  });

  protected close(): void {
    if (!this.isOpen()) return;
    this.isOpen.set(false);
    this.closed.emit();
  }

  protected choose(item: NotificationView): void {
    this.close();
    this.select.emit(item);
  }
}
