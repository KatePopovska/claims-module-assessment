import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ClaimsApi } from '../../../api/claims-api.service';
import { ClaimDetail, ClaimStatus } from '../../../api/models';
import { userDisplayName } from '../../../core/auth/mock-users';
import { NotificationService } from '../../../core/notifications/notification.service';
import { claimStatusLabel } from '../../../shared/claim-status/claim-status';
import { eventTypeLabel } from '../../../shared/format';

export const NOTES_MAX_LENGTH = 4000;
const LIFECYCLE: ClaimStatus[] = ['Draft', 'Open', 'UnderInvestigation', 'PendingPayment', 'Closed'];

interface LifecycleStep {
  status: ClaimStatus;
  state: 'done' | 'current' | 'upcoming';
}

export function lifecycleSteps(status: ClaimStatus): LifecycleStep[] {
  if (status === 'Withdrawn') {
    return [
      { status: 'Draft', state: 'done' },
      { status: 'Withdrawn', state: 'current' },
    ];
  }

  const currentIndex = LIFECYCLE.indexOf(status === 'Reopened' ? 'Open' : status);
  return LIFECYCLE.map((s, i) => ({ status: s, state: i < currentIndex ? 'done' : i === currentIndex ? 'current' : 'upcoming' }));
}

@Component({
  selector: 'app-overview-tab',
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  templateUrl: './overview-tab.html',
  styleUrl: './overview-tab.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverviewTab {
  private readonly claimsApi = inject(ClaimsApi);
  private readonly notifications = inject(NotificationService);

  readonly claim = input.required<ClaimDetail>();
  readonly changed = output<void>();
  readonly viewAuditLog = output<void>();

  protected readonly notesMaxLength = NOTES_MAX_LENGTH;
  protected readonly notes = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(NOTES_MAX_LENGTH)] });
  protected readonly savingNotes = signal(false);
  private readonly notesValue = toSignal(this.notes.valueChanges, { initialValue: '' });
  private readonly savedNotes = signal<string | null>(null);
  protected readonly notesDirty = computed(() => this.notesValue() !== (this.savedNotes() ?? this.claim().notes ?? ''));

  protected readonly lifecycle = computed(() => lifecycleSteps(this.claim().status));
  protected readonly recentActivity = computed(() => this.claim().recentAuditEntries.slice(0, 5));
  protected readonly statusLabel = claimStatusLabel;
  protected readonly eventLabel = eventTypeLabel;
  protected readonly userName = userDisplayName;

  constructor() {
    effect(() => {
      const notes = this.claim().notes ?? '';
      untracked(() => {
        this.savedNotes.set(null);
        this.notes.setValue(notes);
      });
    });
  }

  protected resetNotes(): void {
    this.notes.setValue(this.savedNotes() ?? this.claim().notes ?? '');
  }

  protected saveNotes(): void {
    if (this.notes.invalid || this.savingNotes()) {
      this.notes.markAsTouched();
      return;
    }

    const notes = this.notes.value.trim();
    this.savingNotes.set(true);
    this.claimsApi.updateNotes(this.claim().id, notes || null).subscribe({
      next: () => {
        this.savingNotes.set(false);
        this.savedNotes.set(notes);
        this.notes.setValue(notes);
        this.notifications.success('Claim notes saved.');
        this.changed.emit();
      },
      error: () => this.savingNotes.set(false),
    });
  }
}
