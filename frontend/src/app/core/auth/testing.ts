import type { Role, Schemas } from '../api/api';
import { SESSION_STORAGE_KEY } from './auth.store';

/** Test helper: a login response for the given role. */
export function session(role: Role, expiresInMs = 60_000): Schemas['LoginResponse'] {
  return {
    accessToken: `token-${role}`,
    expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
    user: {
      id: '00000000-0000-0000-0000-000000000001',
      email: 'u@claimdesk.test',
      displayName: 'U',
      role,
    },
  };
}

/** Test helper: put a session in storage before AuthStore is created. */
export function storeSession(value: unknown): void {
  localStorage.setItem(
    SESSION_STORAGE_KEY,
    typeof value === 'string' ? value : JSON.stringify(value),
  );
}
