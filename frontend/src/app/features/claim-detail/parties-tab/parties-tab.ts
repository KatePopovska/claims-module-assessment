import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { filter, startWith, switchMap } from 'rxjs';
import { ClaimsApi } from '../../../api/claims-api.service';
import { ClaimDetail, ClaimParty, PARTY_ROLES, PartyRole, PartyType } from '../../../api/models';
import { NotificationService } from '../../../core/notifications/notification.service';
import { ConfirmDialog, ConfirmDialogData } from '../../../shared/confirm-dialog/confirm-dialog';
import { partyDisplayName, partyRoleLabel } from '../../../shared/format';

export function isLastActiveClaimant(parties: ClaimParty[], party: ClaimParty): boolean {
  return party.isActive && party.partyRole === 'Claimant' && parties.filter((p) => p.isActive && p.partyRole === 'Claimant').length === 1;
}

@Component({
  selector: 'app-parties-tab',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, MatTooltipModule],
  templateUrl: './parties-tab.html',
  styleUrl: './parties-tab.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartiesTab {
  private readonly claimsApi = inject(ClaimsApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  readonly claim = input.required<ClaimDetail>();
  readonly changed = output<void>();

  protected readonly adding = signal(false);
  protected readonly saving = signal(false);
  protected readonly removingId = signal<string | null>(null);
  protected readonly rows = computed(() => {
    const parties = this.claim().parties;
    return [...parties]
      .sort((a, b) => Number(b.isActive) - Number(a.isActive))
      .map((party) => ({
        party,
        name: partyDisplayName(party),
        roleLabel: partyRoleLabel(party.partyRole),
        removable: party.isActive && !isLastActiveClaimant(parties, party),
      }));
  });
  protected readonly roleOptions = PARTY_ROLES.map((role) => ({ role, label: partyRoleLabel(role) }));

  protected readonly form = new FormGroup({
    partyRole: new FormControl<PartyRole>('Witness', { nonNullable: true }),
    partyType: new FormControl<PartyType>('Person', { nonNullable: true }),
    firstName: new FormControl('', { nonNullable: true }),
    lastName: new FormControl('', { nonNullable: true }),
    companyName: new FormControl('', { nonNullable: true }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.email, Validators.maxLength(255)] }),
    phone: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(50)] }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
  });

  constructor() {
    const { partyType, firstName, lastName, companyName } = this.form.controls;
    partyType.valueChanges.pipe(startWith(partyType.value), takeUntilDestroyed()).subscribe((type) => {
      const person = type === 'Person';
      firstName.setValidators(person ? [Validators.required, Validators.maxLength(255)] : []);
      lastName.setValidators(person ? [Validators.required, Validators.maxLength(255)] : []);
      companyName.setValidators(person ? [] : [Validators.required, Validators.maxLength(255)]);
      for (const control of [firstName, lastName, companyName]) {
        control.updateValueAndValidity({ emitEvent: false });
      }
    });
  }

  protected addParty(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const person = value.partyType === 'Person';
    this.saving.set(true);
    this.claimsApi
      .addParty(this.claim().id, {
        partyRole: value.partyRole,
        partyType: value.partyType,
        firstName: person ? value.firstName.trim() : null,
        lastName: person ? value.lastName.trim() : null,
        companyName: person ? null : value.companyName.trim(),
        email: value.email.trim() || null,
        phone: value.phone.trim() || null,
        notes: value.notes.trim() || null,
      })
      .subscribe({
        next: (party) => {
          this.saving.set(false);
          this.adding.set(false);
          this.form.reset({ partyRole: 'Witness', partyType: 'Person' });
          this.notifications.success(`${partyRoleLabel(party.partyRole)} ${partyDisplayName(party)} added.`);
          this.changed.emit();
        },
        error: () => this.saving.set(false),
      });
  }

  protected removeParty(party: ClaimParty): void {
    const name = partyDisplayName(party);
    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
        data: {
          title: `Remove ${name}?`,
          message: 'The party is marked inactive and stays visible in the claim history.',
          confirmLabel: 'Remove party',
        },
      })
      .afterClosed()
      .pipe(
        filter((confirmed) => confirmed === true),
        switchMap(() => {
          this.removingId.set(party.id);
          return this.claimsApi.removeParty(this.claim().id, party.id);
        }),
      )
      .subscribe({
        next: () => {
          this.removingId.set(null);
          this.notifications.success(`${name} removed.`);
          this.changed.emit();
        },
        error: () => this.removingId.set(null),
      });
  }
}
