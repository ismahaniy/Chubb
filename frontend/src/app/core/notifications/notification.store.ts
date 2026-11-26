import { Injectable, computed, effect, inject, untracked } from '@angular/core';
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
  /** True once a list has loaded for this session (stays true while a poll refreshes it). */
  readonly loaded = computed(() => this.list.hasValue());
  /** Newest first, as the API returns them. hasValue() guards value(), which throws while in error. */
  readonly items = computed<NotificationView[]>(() =>
    this.list.hasValue() ? (this.list.value() ?? []) : [],
  );
  readonly unreadCount = computed(() => this.items().filter((n) => n.readAt === null).length);

  /** Newest notification time per claim; see reloadOnNotification. */
  private readonly latestByClaim = computed(() => {
    const latest = new Map<string, string>();
    for (const n of this.items()) if (!latest.has(n.claimId)) latest.set(n.claimId, n.createdAt);
    return latest;
  });

  constructor() {
    interval(NOTIFICATION_POLL_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.list.reload());
  }

  /** Newest notification time about the claim. */
  latestFor(claimId: string): string | undefined {
    return this.latestByClaim().get(claimId);
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

/**
 * Reloads a claim view when a newer notification about that claim arrives: notifications are written from the very
 * events that change a claim, so this keeps an open claim current without polling it. Call in an injection context.
 */
export function reloadOnNotification(claimId: () => string | undefined, reload: () => void): void {
  const notifications = inject(NotificationStore);
  let last: { id: string; at: string | null } | null = null;

  effect(() => {
    const id = claimId();
    // Nothing to compare while the list is loading or failed; the last known state is kept for when it is back.
    if (id === undefined || !notifications.loaded()) return;
    const at = notifications.latestFor(id) ?? null; // null = loaded, but nothing about this claim yet
    if (last?.id === id && at !== null && at !== last.at) untracked(reload);
    last = { id, at };
  });
}
