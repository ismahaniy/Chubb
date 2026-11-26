import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import type { Role } from './core/api/api';
import { AuthStore } from './core/auth/auth.store';
import { ROLE_HOME } from './core/auth/role-home';
import { ToastStore } from './core/errors/toast.store';
import { AppNav, NavLink } from './shared/ui/app-nav';
import { ToastList } from './shared/ui/toast-list';

/** Only routes that exist; extend per feature slice. */
const NAV_LINKS: Record<Role, readonly NavLink[]> = {
  Claimant: [{ label: 'My claims', path: ROLE_HOME.Claimant }],
  Officer: [{ label: 'Work queue', path: ROLE_HOME.Officer }],
  Manager: [{ label: 'Overview', path: ROLE_HOME.Manager }],
};

@Component({
  imports: [RouterOutlet, AppNav, ToastList],
  selector: 'app-root',
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthStore);
  protected readonly toasts = inject(ToastStore);

  protected readonly links = computed(() => {
    const role = this.auth.role();
    return role ? NAV_LINKS[role] : [];
  });

  protected signOut(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
