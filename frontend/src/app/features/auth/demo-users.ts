import type { Role } from '../../core/api/api';

/**
 * Mirrors the users seeded by backend/src/Claims.Infrastructure/Persistence/DbSeeder.cs
 * (and DbSeeder.DemoPassword). There is no API listing them; keep the two in sync by hand.
 */
export interface DemoUser {
  email: string;
  displayName: string;
  role: Role;
}

export const DEMO_PASSWORD = 'Passw0rd!';

export const DEMO_USERS: readonly DemoUser[] = [
  { email: 'claimant1@claimdesk.test', displayName: 'Aisha Rahman', role: 'Claimant' },
  { email: 'claimant2@claimdesk.test', displayName: 'Daniel Wong', role: 'Claimant' },
  { email: 'officer1@claimdesk.test', displayName: 'Priya Menon', role: 'Officer' },
  { email: 'officer2@claimdesk.test', displayName: 'Kenji Sato', role: 'Officer' },
  { email: 'officer3@claimdesk.test', displayName: 'Nadia Halim', role: 'Officer' },
  { email: 'officer4@claimdesk.test', displayName: 'Marco Reyes', role: 'Officer' },
  { email: 'manager@claimdesk.test', displayName: 'Grace Lim', role: 'Manager' },
];
