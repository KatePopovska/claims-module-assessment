import { computed, Injectable, signal } from '@angular/core';
import { MOCK_USERS, MockUser } from './mock-users';

const STORAGE_KEY = 'claims-portal.user-id';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly currentUserSignal = signal<MockUser>(this.restoreUser());

  readonly users = MOCK_USERS;
  readonly currentUser = this.currentUserSignal.asReadonly();
  readonly role = computed(() => this.currentUserSignal().role);
  readonly canApproveReserves = computed(() => this.role() === 'supervisor' || this.role() === 'manager');
  readonly isManager = computed(() => this.role() === 'manager');

  switchUser(userId: string): void {
    const user = MOCK_USERS.find((u) => u.id === userId);
    if (!user) {
      return;
    }

    this.currentUserSignal.set(user);
    try {
      localStorage.setItem(STORAGE_KEY, user.id);
    } catch {
      return;
    }
  }

  token(): string {
    const { id, role } = this.currentUserSignal();
    return btoa(JSON.stringify({ userId: id, role }));
  }

  private restoreUser(): MockUser {
    try {
      const storedId = localStorage.getItem(STORAGE_KEY);
      return MOCK_USERS.find((u) => u.id === storedId) ?? MOCK_USERS[0];
    } catch {
      return MOCK_USERS[0];
    }
  }
}
