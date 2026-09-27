import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { startWith } from 'rxjs';
import { ASSET_TYPES, AssetType, CreateClaimParty, CreateClaimRiskObject, PARTY_ROLES, PartyRole, PartyType } from '../../../api/models';
import { PartiesForm } from '../fnol-form';

@Component({
  selector: 'app-parties-step',
  imports: [ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  templateUrl: './parties-step.html',
  styleUrl: './parties-step.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartiesStep implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  readonly form = input.required<PartiesForm>();

  protected readonly partyRoles = PARTY_ROLES;
  protected readonly assetTypes = ASSET_TYPES;
  protected readonly addingParty = signal(true);
  protected readonly addingRiskObject = signal(false);

  protected readonly partyDraft = new FormGroup({
    partyRole: new FormControl<PartyRole>('Claimant', { nonNullable: true }),
    partyType: new FormControl<PartyType>('Person', { nonNullable: true }),
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(255)] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(255)] }),
    companyName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(255)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.email, Validators.maxLength(255)] }),
    phone: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(50)] }),
  });

  protected readonly riskObjectDraft = new FormGroup({
    assetType: new FormControl<AssetType>('Vehicle', { nonNullable: true }),
    assetDescription: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] }),
    damageDescription: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    assetReference: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(255)] }),
    isPrimary: new FormControl(false, { nonNullable: true }),
  });

  ngOnInit(): void {
    const { partyType, firstName, lastName, companyName } = this.partyDraft.controls;

    partyType.valueChanges.pipe(startWith(partyType.value), takeUntilDestroyed(this.destroyRef)).subscribe((type) => {
      const person = type === 'Person';
      for (const control of [firstName, lastName]) {
        control.setValidators(person ? [Validators.required, Validators.maxLength(255)] : [Validators.maxLength(255)]);
        control.updateValueAndValidity({ emitEvent: false });
      }
      companyName.setValidators(person ? [Validators.maxLength(255)] : [Validators.required, Validators.maxLength(255)]);
      companyName.updateValueAndValidity({ emitEvent: false });
    });
  }

  protected get parties(): CreateClaimParty[] {
    return this.form().controls.parties.getRawValue();
  }

  protected get riskObjects(): CreateClaimRiskObject[] {
    return this.form().controls.riskObjects.getRawValue();
  }

  protected addParty(): void {
    if (this.partyDraft.invalid) {
      this.partyDraft.markAllAsTouched();
      return;
    }

    const draft = this.partyDraft.getRawValue();
    const person = draft.partyType === 'Person';
    this.form().controls.parties.push(
      new FormControl<CreateClaimParty>(
        {
          partyRole: draft.partyRole,
          partyType: draft.partyType,
          firstName: person ? draft.firstName.trim() : null,
          lastName: person ? draft.lastName.trim() : null,
          companyName: person ? null : draft.companyName.trim(),
          email: draft.email.trim() || null,
          phone: draft.phone.trim() || null,
        },
        { nonNullable: true },
      ),
    );
    this.form().controls.parties.markAsTouched();

    this.partyDraft.reset({ partyRole: 'Witness', partyType: 'Person' });
    this.addingParty.set(false);
  }

  protected removeParty(index: number): void {
    this.form().controls.parties.removeAt(index);
    this.form().controls.parties.markAsTouched();
  }

  protected addRiskObject(): void {
    if (this.riskObjectDraft.invalid) {
      this.riskObjectDraft.markAllAsTouched();
      return;
    }

    const draft = this.riskObjectDraft.getRawValue();
    const riskObjects = this.form().controls.riskObjects;
    riskObjects.push(
      new FormControl<CreateClaimRiskObject>(
        {
          assetType: draft.assetType,
          assetDescription: draft.assetDescription.trim(),
          damageDescription: draft.damageDescription.trim() || null,
          assetReference: draft.assetReference.trim() || null,
          isPrimary: draft.isPrimary || riskObjects.length === 0,
        },
        { nonNullable: true },
      ),
    );

    this.riskObjectDraft.reset({ assetType: 'Vehicle', isPrimary: false });
    this.addingRiskObject.set(false);
  }

  protected removeRiskObject(index: number): void {
    this.form().controls.riskObjects.removeAt(index);
  }

  protected partyName(party: CreateClaimParty): string {
    return party.partyType === 'Company' ? (party.companyName ?? '') : `${party.firstName ?? ''} ${party.lastName ?? ''}`.trim();
  }

  protected roleLabel(role: PartyRole): string {
    return role === 'ThirdParty' ? 'Third Party' : role;
  }
}
