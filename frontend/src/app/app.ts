import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import type { Role, Schemas } from './core/api/api';
import { AuthStore } from './core/auth/auth.store';
import { CLAIM_PATH, ROLE_HOME } from './core/auth/role-home';
import { ToastStore } from './core/errors/toast.store';
import { NotificationStore } from './core/notifications/notification.store';
import { AppNav, NavLink } from './shared/ui/app-nav';
import { NotificationBell } from './shared/ui/notification-bell';
import { ToastList } from './shared/ui/toast-list';

/** Only routes that exist; extend per feature slice. */
const NAV_LINKS: Record<Role, readonly NavLink[]> = {
  Claimant: [{ label: 'My claims', path: ROLE_HOME.Claimant }],
  Officer: [{ label: 'Work queue', path: ROLE_HOME.Officer }],
  Manager: [{ label: 'Dashboard', path: ROLE_HOME.Manager }],
};

@Component({
  imports: [RouterOutlet, AppNav, NotificationBell, ToastList],
  selector: 'app-root',
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthStore);
  protected readonly toasts = inject(ToastStore);
  protected readonly notifications = inject(NotificationStore);

  protected readonly links = computed(() => {
    const role = this.auth.role();
    return role ? NAV_LINKS[role] : [];
  });

  /** Managers receive no notifications and have no claim page, so their items are not links. */
  protected readonly claimPath = computed(() => {
    const role = this.auth.role();
    return role ? CLAIM_PATH[role] : null;
  });

  protected openClaim(notification: Schemas['NotificationView']): void {
    const path = this.claimPath();
    if (path) void this.router.navigateByUrl(path(notification.claimId));
  }

  protected signOut(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
