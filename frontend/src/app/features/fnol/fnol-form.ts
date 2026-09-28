import { AbstractControl, FormArray, FormControl, FormGroup, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { merge, Subscription } from 'rxjs';
import {
  CauseOfLossCode,
  CreateClaimParty,
  CreateClaimRequest,
  CreateClaimRiskObject,
  PolicySearchResult,
  ReserveComponentType,
} from '../../api/models';

export const LOSS_DESCRIPTION_MIN_LENGTH = 20;

export type PolicyLossForm = FormGroup<{
  unknownPolicy: FormControl<boolean>;
  policy: FormControl<PolicySearchResult | string | null>;
  lossDate: FormControl<Date | null>;
  lossTime: FormControl<Date | null>;
  causeOfLoss: FormControl<CauseOfLossCode | string | null>;
  lossDescription: FormControl<string>;
  lossLocation: FormControl<string>;
  estimatedLossAmount: FormControl<number | null>;
}>;

export type PartiesForm = FormGroup<{
  parties: FormArray<FormControl<CreateClaimParty>>;
  riskObjects: FormArray<FormControl<CreateClaimRiskObject>>;
}>;

export type ReserveForm = FormGroup<{
  enabled: FormControl<boolean>;
  component: FormControl<ReserveComponentType | null>;
  amount: FormControl<number | null>;
  changeReason: FormControl<string>;
}>;

export interface FnolForm {
  policyLoss: PolicyLossForm;
  parties: PartiesForm;
  reserve: ReserveForm;
}

export const notInFuture: ValidatorFn = (control: AbstractControl<Date | null>): ValidationErrors | null =>
  control.value instanceof Date && control.value.getTime() > Date.now() ? { future: true } : null;

export const selectedOption: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  control.value !== null && typeof control.value !== 'object' ? { notSelected: true } : null;

export const atLeastOneClaimant: ValidatorFn = (control: AbstractControl<CreateClaimParty[]>): ValidationErrors | null =>
  (control.value ?? []).some((p) => p.partyRole === 'Claimant') ? null : { claimantRequired: true };

export const maxFourDecimals: ValidatorFn = (control: AbstractControl<number | null>): ValidationErrors | null =>
  control.value !== null && !/^-?\d+(\.\d{1,4})?$/.test(String(control.value)) ? { decimals: true } : null;

export const reserveAmountRule: ValidatorFn = (control: AbstractControl<number | null>): ValidationErrors | null => {
  const component = control.parent?.get('component')?.value as ReserveComponentType | null | undefined;
  const amount = control.value;
  if (amount === null || amount === undefined) {
    return null;
  }

  const valid = component === 'SubrogationRecoverable' ? amount !== 0 : amount > 0;
  return valid ? null : { amountRule: true };
};

export function createFnolForm(): FnolForm {
  return {
    policyLoss: new FormGroup({
      unknownPolicy: new FormControl(false, { nonNullable: true }),
      policy: new FormControl<PolicySearchResult | string | null>(null, [Validators.required, selectedOption]),
      lossDate: new FormControl<Date | null>(null, [Validators.required, notInFuture]),
      lossTime: new FormControl<Date | null>(null),
      causeOfLoss: new FormControl<CauseOfLossCode | string | null>(null, [Validators.required, selectedOption]),
      lossDescription: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(LOSS_DESCRIPTION_MIN_LENGTH)] }),
      lossLocation: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
      estimatedLossAmount: new FormControl<number | null>(null, [Validators.min(0), maxFourDecimals]),
    }),
    parties: new FormGroup({
      parties: new FormArray<FormControl<CreateClaimParty>>([], [atLeastOneClaimant]),
      riskObjects: new FormArray<FormControl<CreateClaimRiskObject>>([]),
    }),
    reserve: new FormGroup({
      enabled: new FormControl(false, { nonNullable: true }),
      component: new FormControl<ReserveComponentType | null>({ value: null, disabled: true }, [Validators.required]),
      amount: new FormControl<number | null>({ value: null, disabled: true }, [Validators.required, maxFourDecimals, reserveAmountRule]),
      changeReason: new FormControl({ value: '', disabled: true }, { nonNullable: true, validators: [Validators.maxLength(1000)] }),
    }),
  };
}

export function withTime(date: Date, time: Date | null): Date {
  const combined = new Date(date);
  combined.setHours(time?.getHours() ?? 0, time?.getMinutes() ?? 0, 0, 0);
  return combined;
}

export function syncLossDateAndTime(form: PolicyLossForm): Subscription {
  const { lossDate, lossTime } = form.controls;

  return merge(lossDate.valueChanges, lossTime.valueChanges).subscribe(() => {
    const date = lossDate.value;
    if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
      return;
    }

    const combined = withTime(date, lossTime.value);
    if (combined.getTime() !== date.getTime()) {
      lossDate.setValue(combined);
    }
  });
}

export function selectedPolicy(form: PolicyLossForm): PolicySearchResult | null {
  const { unknownPolicy, policy } = form.getRawValue();
  return !unknownPolicy && policy !== null && typeof policy === 'object' ? policy : null;
}

export type PolicyCoverState = 'in-force' | 'outside-period' | 'expired' | 'no-loss-date';

export function policyCoverState(policy: PolicySearchResult, lossDate: Date | null): PolicyCoverState {
  if (policy.status !== 'Active') {
    return 'expired';
  }

  if (!lossDate) {
    return 'no-loss-date';
  }

  return isWithinPolicyPeriod(policy, lossDate) ? 'in-force' : 'outside-period';
}

export function isWithinPolicyPeriod(policy: PolicySearchResult, lossDate: Date): boolean {
  const time = lossDate.getTime();
  return time >= new Date(policy.effectiveDate).getTime() && time <= new Date(policy.expirationDate).getTime();
}

export function intakeWarnings(form: FnolForm): string[] {
  const warnings: string[] = [];
  const policy = selectedPolicy(form.policyLoss);
  const lossDate = form.policyLoss.controls.lossDate.value;

  if (form.policyLoss.controls.unknownPolicy.value) {
    warnings.push('Policy unknown: the claim is created without a policy link, and reserves cannot be set until a policy is associated.');
  } else if (policy && lossDate && !isWithinPolicyPeriod(policy, lossDate)) {
    warnings.push(`The loss date is outside the policy's effective period (${policy.effectiveDate.slice(0, 10)} to ${policy.expirationDate.slice(0, 10)}).`);
  }

  if (form.parties.controls.riskObjects.length === 0) {
    warnings.push('No risk objects are linked to the claim.');
  }

  return warnings;
}

export function toCreateClaimRequest(form: FnolForm): CreateClaimRequest {
  const loss = form.policyLoss.getRawValue();
  const reserve = form.reserve.getRawValue();
  const cause = loss.causeOfLoss as CauseOfLossCode;
  const policy = selectedPolicy(form.policyLoss);

  return {
    policyId: policy?.id ?? null,
    lossDate: (loss.lossDate as Date).toISOString(),
    lossDescription: loss.lossDescription.trim(),
    lossLocation: loss.lossLocation.trim() || null,
    causeOfLossCode: cause.code,
    estimatedLossAmount: loss.estimatedLossAmount,
    policeReportNumber: null,
    parties: form.parties.controls.parties.getRawValue(),
    riskObjects: form.parties.controls.riskObjects.getRawValue(),
    initialReserve:
      reserve.enabled && policy && reserve.component && reserve.amount !== null
        ? { component: reserve.component, amount: reserve.amount, changeReason: reserve.changeReason.trim() || null }
        : null,
  };
}

const STEP_ONE_FIELDS = ['policyid', 'lossdate', 'lossdescription', 'losslocation', 'causeoflosscode', 'estimatedlossamount'];

export function stepForErrorKey(key: string): number {
  const normalised = key.toLowerCase();
  if (normalised.startsWith('parties') || normalised.startsWith('riskobjects')) {
    return 1;
  }

  return STEP_ONE_FIELDS.includes(normalised) ? 0 : 2;
}

export function errorsByStep(errors: Record<string, string[]>): string[][] {
  const byStep: string[][] = [[], [], []];
  for (const [key, messages] of Object.entries(errors)) {
    byStep[stepForErrorKey(key)].push(...messages);
  }

  return byStep;
}
