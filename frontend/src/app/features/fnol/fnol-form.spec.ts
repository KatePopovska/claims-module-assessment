import { FormControl } from '@angular/forms';
import { CauseOfLossCode, CreateClaimParty, PolicySearchResult } from '../../api/models';
import {
  atLeastOneClaimant,
  createFnolForm,
  errorsByStep,
  intakeWarnings,
  notInFuture,
  policyCoverState,
  toCreateClaimRequest,
} from './fnol-form';

const policy: PolicySearchResult = {
  id: '20000000-0000-0000-0000-000000000001',
  policyNumber: 'POL-2024-001001',
  clientName: 'Meridian Transport LLC',
  effectiveDate: '2024-01-01T00:00:00+00:00',
  expirationDate: '2026-12-31T00:00:00+00:00',
  status: 'Active',
  coverageTypes: 'Vehicle, Cargo',
};

const cause: CauseOfLossCode = { code: 'COL-FIRE', name: 'Fire', perilCategory: 'Property', sortOrder: 1 };

const claimant: CreateClaimParty = { partyRole: 'Claimant', partyType: 'Person', firstName: 'Ann', lastName: 'Lee', companyName: null, email: null, phone: null };

function filledForm() {
  const form = createFnolForm();
  form.policyLoss.patchValue({
    policy,
    lossDate: new Date('2025-06-15T10:30:00Z'),
    causeOfLoss: cause,
    lossDescription: '  Fire in the loading bay destroyed two pallets.  ',
    lossLocation: 'Warehouse 4',
    estimatedLossAmount: 12_500,
  });
  form.parties.controls.parties.push(new FormControl(claimant, { nonNullable: true }));
  return form;
}

describe('FNOL validators', () => {
  it('rejects loss dates in the future', () => {
    expect(notInFuture(new FormControl(new Date(Date.now() + 60_000)))).toEqual({ future: true });
    expect(notInFuture(new FormControl(new Date(Date.now() - 60_000)))).toBeNull();
  });

  it('requires at least one Claimant party', () => {
    expect(atLeastOneClaimant(new FormControl([{ ...claimant, partyRole: 'Witness' }]))).toEqual({ claimantRequired: true });
    expect(atLeastOneClaimant(new FormControl([claimant]))).toBeNull();
  });

  it('needs a selected policy and cause, not free text', () => {
    const form = createFnolForm();
    form.policyLoss.controls.policy.setValue('POL-2024');
    form.policyLoss.controls.causeOfLoss.setValue('fire');

    expect(form.policyLoss.controls.policy.hasError('notSelected')).toBe(true);
    expect(form.policyLoss.controls.causeOfLoss.hasError('notSelected')).toBe(true);
  });

  it('requires a 20 character description', () => {
    const form = createFnolForm();
    form.policyLoss.controls.lossDescription.setValue('Too short');

    expect(form.policyLoss.controls.lossDescription.hasError('minlength')).toBe(true);
  });

  it('applies the positive-amount rule except for subrogation', () => {
    const { reserve } = createFnolForm();
    reserve.enable();
    reserve.patchValue({ component: 'Indemnity', amount: -100 });
    expect(reserve.controls.amount.hasError('amountRule')).toBe(true);

    reserve.controls.component.setValue('SubrogationRecoverable');
    reserve.controls.amount.updateValueAndValidity();
    expect(reserve.controls.amount.hasError('amountRule')).toBe(false);
  });
});

describe('policyCoverState', () => {
  it('is in force when the loss date is within the policy period', () => {
    expect(policyCoverState(policy, new Date('2025-06-15'))).toBe('in-force');
  });

  it('flags loss dates outside the period', () => {
    expect(policyCoverState(policy, new Date('2023-06-15'))).toBe('outside-period');
  });

  it('flags expired policies', () => {
    expect(policyCoverState({ ...policy, status: 'Expired' }, new Date('2025-06-15'))).toBe('expired');
  });

  it('waits for a loss date', () => {
    expect(policyCoverState(policy, null)).toBe('no-loss-date');
  });
});

describe('intakeWarnings', () => {
  it('warns about missing risk objects only when the policy covers the loss date', () => {
    expect(intakeWarnings(filledForm())).toEqual(['No risk objects are linked to the claim.']);
  });

  it('warns when the policy is unknown', () => {
    const form = filledForm();
    form.policyLoss.controls.unknownPolicy.setValue(true);

    expect(intakeWarnings(form)[0]).toContain('Policy unknown');
  });

  it('warns when the loss date is outside the policy period', () => {
    const form = filledForm();
    form.policyLoss.controls.lossDate.setValue(new Date('2023-02-01T00:00:00Z'));

    expect(intakeWarnings(form)[0]).toContain("outside the policy's effective period");
  });
});

describe('toCreateClaimRequest', () => {
  it('maps the form to the API request', () => {
    const request = toCreateClaimRequest(filledForm());

    expect(request).toMatchObject({
      policyId: policy.id,
      lossDate: '2025-06-15T10:30:00.000Z',
      lossDescription: 'Fire in the loading bay destroyed two pallets.',
      lossLocation: 'Warehouse 4',
      causeOfLossCode: 'COL-FIRE',
      estimatedLossAmount: 12_500,
      parties: [claimant],
      riskObjects: [],
      initialReserve: null,
    });
  });

  it('includes the initial reserve when enabled', () => {
    const form = filledForm();
    form.reserve.enable();
    form.reserve.patchValue({ enabled: true, component: 'Indemnity', amount: 5_000, changeReason: ' Initial estimate ' });

    expect(toCreateClaimRequest(form).initialReserve).toEqual({ component: 'Indemnity', amount: 5_000, changeReason: 'Initial estimate' });
  });

  it('never sends a reserve without a policy', () => {
    const form = filledForm();
    form.reserve.enable();
    form.reserve.patchValue({ enabled: true, component: 'Indemnity', amount: 5_000 });
    form.policyLoss.controls.unknownPolicy.setValue(true);

    const request = toCreateClaimRequest(form);
    expect(request.policyId).toBeNull();
    expect(request.initialReserve).toBeNull();
  });
});

describe('errorsByStep', () => {
  it('routes server validation errors to the step that owns the field', () => {
    const byStep = errorsByStep({
      LossDate: ['Loss date cannot be in the future.'],
      'Parties[0].FirstName': ['First name is required for a person party.'],
      'RiskObjects[0].AssetDescription': ['Asset description is required.'],
      'InitialReserve.Amount': ['Too many decimals.'],
    });

    expect(byStep).toEqual([
      ['Loss date cannot be in the future.'],
      ['First name is required for a person party.', 'Asset description is required.'],
      ['Too many decimals.'],
    ]);
  });
});
