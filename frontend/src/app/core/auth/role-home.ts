import type { Role } from '../api/api';

/** Landing route per role. `Record<Role, …>` makes the compiler flag any role added to the API. */
export const ROLE_HOME: Record<Role, string> = {
  Claimant: '/claimant',
  Officer: '/officer',
  Manager: '/manager',
};

export function isRole(value: unknown): value is Role {
  return typeof value === 'string' && Object.hasOwn(ROLE_HOME, value);
}
