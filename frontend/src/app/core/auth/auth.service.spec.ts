import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { initials, MOCK_USERS, UNKNOWN_USER_NAME, userDisplayName } from './mock-users';

describe('AuthService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  it('starts as the handler when nothing is stored', () => {
    const auth = TestBed.inject(AuthService);

    expect(auth.currentUser().role).toBe('handler');
    expect(auth.canApproveReserves()).toBe(false);
  });

  it('switches user, updates role flags and remembers the choice', () => {
    const auth = TestBed.inject(AuthService);
    const manager = MOCK_USERS.find((u) => u.role === 'manager')!;

    auth.switchUser(manager.id);

    expect(auth.role()).toBe('manager');
    expect(auth.canApproveReserves()).toBe(true);
    expect(auth.isManager()).toBe(true);
    expect(localStorage.getItem('claims-portal.user-id')).toBe(manager.id);
  });

  it('restores the stored user on start', () => {
    const supervisor = MOCK_USERS.find((u) => u.role === 'supervisor')!;
    localStorage.setItem('claims-portal.user-id', supervisor.id);

    expect(TestBed.inject(AuthService).currentUser().id).toBe(supervisor.id);
  });

  it('ignores unknown user ids', () => {
    const auth = TestBed.inject(AuthService);

    auth.switchUser('00000000-0000-0000-0000-000000000000');

    expect(auth.role()).toBe('handler');
  });

  it('issues the base64 JSON token the backend mock handler expects', () => {
    const auth = TestBed.inject(AuthService);

    const payload = JSON.parse(atob(auth.token()));

    expect(payload).toEqual({ userId: MOCK_USERS[0].id, role: 'handler' });
  });
});

describe('mock users', () => {
  it('maps known ids case-insensitively and falls back to "Unknown user"', () => {
    expect(userDisplayName(MOCK_USERS[1].id.toUpperCase())).toBe(MOCK_USERS[1].name);
    expect(userDisplayName('99999999-9999-9999-9999-999999999999')).toBe(UNKNOWN_USER_NAME);
    expect(userDisplayName(null)).toBe(UNKNOWN_USER_NAME);
  });

  it('builds initials from first and last word', () => {
    expect(initials('Meridian Transport LLC')).toBe('ML');
    expect(initials('Acme')).toBe('A');
    expect(initials(null)).toBe('?');
  });
});
