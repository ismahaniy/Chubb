import { Injectable, computed, inject } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom, interval } from 'rxjs';
import type { Schemas } from '../api/api';
import { AuthStore } from '../auth/auth.store';
import { NotificationsApi } from './notifications.api';

export const NOTIFICATION_POLL_MS = 15_000;

type NotificationView = Schemas['NotificationView'];

/**
 * The signed-in user's notifications, polled for the whole session.
 *
 * The list is keyed on the access token: signed out it is idle and empty, and a new sign-in starts a fresh load, so
 * one user's notifications never show for the next. Read state lives on the server (readAt), so it follows the user
 * across browsers.
 */
@Injectable({ providedIn: 'root' })
export class NotificationStore {
  private readonly api = inject(NotificationsApi);
  private readonly auth = inject(AuthStore);

  private readonly list = rxResource({
    params: () => this.auth.token() ?? undefined,
    stream: () => this.api.list(),
  });

  readonly failed = computed(() => this.list.status() === 'error');
  /** Newest first, as the API returns them. hasValue() guards value(), which throws while in error. */
  readonly items = computed<NotificationView[]>(() =>
    this.list.hasValue() ? (this.list.value() ?? []) : [],
  );
  readonly unreadCount = computed(() => this.items().filter((n) => n.readAt === null).length);

  constructor() {
    interval(NOTIFICATION_POLL_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.list.reload());
  }

  /**
   * Marks everything shown as read. `upTo` is the newest createdAt on screen (server time, so no clock skew), so a
   * notification that arrives meanwhile stays unread. Reloads either way to show the server's state.
   */
  async markAllRead(): Promise<void> {
    const newest = this.items()[0];
    if (newest === undefined || this.unreadCount() === 0) return;
    try {
      await firstValueFrom(this.api.markRead({ upTo: newest.createdAt }));
    } catch {
      // Not toasted (silent): the badge simply stays until the next attempt.
    } finally {
      this.list.reload();
    }
  }
}

