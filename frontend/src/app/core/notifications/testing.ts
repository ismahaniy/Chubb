import type { Schemas } from '../api/api';

/** Test helper: one notification. */
export function notification(
  id: string,
  createdAt: string,
  readAt: string | null = null,
  claimId = 'claim-1',
): Schemas['NotificationView'] {
  return { id, claimId, message: `message ${id}`, createdAt, readAt };
}
