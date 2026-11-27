import type { Role } from '../api/api';

/** Landing route per role. `Record<Role, …>` makes the compiler flag any role added to the API. */
export const ROLE_HOME: Record<Role, string> = {
  Claimant: '/claimant',
  Officer: '/officer',
  Manager: '/manager',
};

/** Where a role opens one claim (e.g. from a notification). Managers have no claim page. */
export const CLAIM_PATH: Record<Role, ((claimId: string) => string) | null> = {
  Claimant: (id) => `/claimant/claims/${id}`,
  Officer: (id) => `/officer/claims/${id}`,
  Manager: null,
};

export function isRole(value: unknown): value is Role {
  return typeof value === 'string' && Object.hasOwn(ROLE_HOME, value);
}
