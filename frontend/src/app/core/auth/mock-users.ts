export type UserRole = 'handler' | 'supervisor' | 'manager';

export interface MockUser {
  id: string;
  name: string;
  role: UserRole;
  roleLabel: string;
}

export const MOCK_USERS: readonly MockUser[] = [
  { id: '11111111-1111-1111-1111-111111111111', name: 'Hannah Reed', role: 'handler', roleLabel: 'Claims Handler' },
  { id: '22222222-2222-2222-2222-222222222222', name: 'Samuel Ortiz', role: 'supervisor', roleLabel: 'Claims Supervisor' },
  { id: '33333333-3333-3333-3333-333333333333', name: 'Maria Chen', role: 'manager', roleLabel: 'Claims Manager' },
];

export const UNKNOWN_USER_NAME = 'Unknown user';

export function userDisplayName(userId: string | null | undefined): string {
  if (!userId) {
    return UNKNOWN_USER_NAME;
  }

  return MOCK_USERS.find((u) => u.id.toLowerCase() === userId.toLowerCase())?.name ?? UNKNOWN_USER_NAME;
}

export function initials(name: string | null | undefined): string {
  const parts = (name ?? '').trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return '?';
  }

  return (parts[0][0] + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase();
}
