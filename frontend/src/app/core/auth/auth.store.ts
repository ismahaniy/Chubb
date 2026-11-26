import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CurrentUser, Schemas } from '../api/api';
import { AuthApi } from './auth.api';
import { isRole } from './role-home';

type Session = Schemas['LoginResponse'];

export const SESSION_STORAGE_KEY = 'claimdesk.session';

/**
 * Signed-in session. Does not navigate: pages decide where to go after login/logout.
 * A token that expires mid-session is handled by the error interceptor's 401 path.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly api = inject(AuthApi);
  private readonly state = signal<Session | null>(readStoredSession());

  readonly session = this.state.asReadonly();
  readonly user = computed(() => this.state()?.user ?? null);
  readonly role = computed(() => this.user()?.role ?? null);
  readonly token = computed(() => this.state()?.accessToken ?? null);
  readonly isAuthenticated = computed(() => this.state() !== null);

  async login(credentials: Schemas['LoginRequest']): Promise<CurrentUser> {
    const session = await firstValueFrom(this.api.login(credentials));
    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
    this.state.set(session);
    return session.user;
  }

  logout(): void {
    localStorage.removeItem(SESSION_STORAGE_KEY);
    this.state.set(null);
  }
}

/** Returns the stored session only if it is well-formed, unexpired and has a known role. */
function readStoredSession(): Session | null {
  const raw = localStorage.getItem(SESSION_STORAGE_KEY);
  if (raw === null) return null;

  try {
    const session = JSON.parse(raw) as Partial<Session> | null;
    const valid =
      typeof session?.accessToken === 'string' &&
      typeof session.expiresAt === 'string' &&
      Date.parse(session.expiresAt) > Date.now() &&
      isRole(session.user?.role);
    if (valid) return session as Session;
  } catch {
    // Corrupt entry: fall through and clear it.
  }

  localStorage.removeItem(SESSION_STORAGE_KEY);
  return null;
}
