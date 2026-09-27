import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { map, merge, startWith, switchMap } from 'rxjs';
import { CauseOfLossCode, CreateClaimParty, RESERVE_COMPONENT_TYPES } from '../../../api/models';
import { ReserveThreshold } from '../../../shared/reserve-threshold/reserve-threshold';
import { FnolForm, intakeWarnings, selectedPolicy } from '../fnol-form';

@Component({
  selector: 'app-review-step',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    ReserveThreshold,
  ],
  templateUrl: './review-step.html',
  styleUrl: './review-step.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewStep {
  readonly form = input.required<FnolForm>();

  private readonly version = toSignal(
    toObservable(this.form).pipe(
      switchMap((form) => merge(form.policyLoss.valueChanges, form.parties.valueChanges, form.reserve.valueChanges).pipe(startWith(null))),
      map((_, index) => index),
    ),
    { initialValue: 0 },
  );

  protected readonly components = RESERVE_COMPONENT_TYPES;

  protected readonly summary = computed(() => {
    this.version();
    const form = this.form();
    const loss = form.policyLoss.getRawValue();

    return {
      policy: selectedPolicy(form.policyLoss),
      unknownPolicy: loss.unknownPolicy,
      lossDate: loss.lossDate,
      cause: loss.causeOfLoss && typeof loss.causeOfLoss === 'object' ? (loss.causeOfLoss as CauseOfLossCode).name : null,
      lossLocation: loss.lossLocation,
      lossDescription: loss.lossDescription,
      estimatedLossAmount: loss.estimatedLossAmount,
      parties: form.parties.controls.parties.getRawValue(),
      riskObjects: form.parties.controls.riskObjects.getRawValue(),
      reserve: form.reserve.getRawValue(),
      warnings: intakeWarnings(form),
    };
  });

  protected readonly reserveAmount = computed(() => {
    this.version();
    return this.form().reserve.controls.amount.value;
  });

  protected partyName(party: CreateClaimParty): string {
    return party.partyType === 'Company' ? (party.companyName ?? '') : `${party.firstName ?? ''} ${party.lastName ?? ''}`.trim();
  }
}
