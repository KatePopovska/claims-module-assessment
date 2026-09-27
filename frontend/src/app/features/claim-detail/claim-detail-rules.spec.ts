import { ClaimDetail, ClaimParty } from '../../api/models';
import { tabForRelatedEntity, DETAIL_TABS } from './claim-detail-tabs';
import { uploadValidationError } from './documents-tab/documents-tab';
import { lifecycleSteps } from './overview-tab/overview-tab';
import { isLastActiveClaimant } from './parties-tab/parties-tab';
import { closureChecklist, isReasonRequired } from './status-change-dialog/status-change-dialog';

function party(overrides: Partial<ClaimParty>): ClaimParty {
  return {
    id: crypto.randomUUID(),
    partyRole: 'Claimant',
    partyType: 'Person',
    firstName: 'Ann',
    lastName: 'Lee',
    companyName: null,
    email: null,
    phone: null,
    notes: null,
    isActive: true,
    ...overrides,
  };
}

function claim(overrides: Partial<ClaimDetail> = {}): ClaimDetail {
  return {
    id: 'claim-1',
    claimNumber: 'CLM-2026-0000001',
    status: 'Open',
    validNextStatuses: ['UnderInvestigation', 'PendingPayment', 'Closed', 'Withdrawn'],
    severity: null,
    policyId: 'policy-1',
    policyNumber: 'POL-2024-001001',
    clientName: 'Meridian Transport LLC',
    reportedDate: '2026-01-10T09:00:00Z',
    assignedHandlerId: '11111111-1111-1111-1111-111111111111',
    closedAt: null,
    closureReason: null,
    notes: null,
    lossEvent: {
      lossDate: '2026-01-09T09:00:00Z',
      lossDescription: 'Rear-end collision on the highway exit ramp.',
      lossLocation: null,
      causeOfLossCode: 'COL-VEH-COL',
      causeOfLossName: 'Vehicle Collision',
      estimatedLossAmount: null,
      reportDate: '2026-01-10T09:00:00Z',
      policeReportNumber: null,
    },
    parties: [party({})],
    riskObjects: [],
    reserves: { totalReserves: 0, components: [] },
    documents: [],
    recentAuditEntries: [],
    ...overrides,
  };
}

describe('closure pre-flight checklist', () => {
  it('passes a clean claim', () => {
    expect(closureChecklist(claim()).every((item) => item.state === 'pass')).toBe(true);
  });

  it('blocks on pending reserve approvals and a missing claimant', () => {
    const items = closureChecklist(
      claim({
        parties: [party({ isActive: false })],
        reserves: { totalReserves: 0, components: [{ id: 'c1', component: 'Indemnity', status: 'Active', currentBalance: 0, pendingAmount: 50_000 }] },
      }),
    );

    expect(items.filter((item) => item.state === 'fail').map((item) => item.label)).toEqual([
      'No reserve transactions are pending approval',
      'At least one active Claimant party',
    ]);
  });

  it('warns and requires a justification when approved reserves remain open', () => {
    const withReserves = claim({ reserves: { totalReserves: 5_000, components: [{ id: 'c1', component: 'Indemnity', status: 'Active', currentBalance: 5_000, pendingAmount: 0 }] } });

    expect(closureChecklist(withReserves).some((item) => item.state === 'warn')).toBe(true);
    expect(isReasonRequired(withReserves, 'Closed')).toBe(true);
    expect(isReasonRequired(claim(), 'Closed')).toBe(false);
  });

  it('requires a reason to withdraw or reopen', () => {
    expect(isReasonRequired(claim(), 'Withdrawn')).toBe(true);
    expect(isReasonRequired(claim(), 'Reopened')).toBe(true);
    expect(isReasonRequired(claim(), 'UnderInvestigation')).toBe(false);
  });
});

describe('isLastActiveClaimant', () => {
  it('protects the only active claimant', () => {
    const only = party({});
    expect(isLastActiveClaimant([only, party({ isActive: false })], only)).toBe(true);
  });

  it('allows removal when another active claimant exists', () => {
    const first = party({});
    expect(isLastActiveClaimant([first, party({})], first)).toBe(false);
    expect(isLastActiveClaimant([first], party({ partyRole: 'Witness' }))).toBe(false);
  });
});

describe('lifecycleSteps', () => {
  it('marks earlier statuses done and the current one current', () => {
    expect(lifecycleSteps('UnderInvestigation').map((s) => s.state)).toEqual(['done', 'done', 'current', 'upcoming', 'upcoming']);
  });

  it('shows the short path for withdrawn claims', () => {
    expect(lifecycleSteps('Withdrawn').map((s) => s.status)).toEqual(['Draft', 'Withdrawn']);
  });
});

describe('uploadValidationError', () => {
  const file = (name: string, size: number) => new File([new Uint8Array(0)], name) as File & { size: number };

  it('accepts allowed types within the size limit', () => {
    expect(uploadValidationError(file('report.PDF', 0))).toBeNull();
  });

  it('rejects other file types', () => {
    expect(uploadValidationError(file('macro.exe', 0))).toContain('only PDF');
  });

  it('rejects files over 50 MB', () => {
    const big = file('photo.jpg', 0);
    Object.defineProperty(big, 'size', { value: 50 * 1024 * 1024 + 1 });
    expect(uploadValidationError(big)).toContain('50 MB');
  });
});

describe('tabForRelatedEntity', () => {
  it('links audit entries to the tab of their entity', () => {
    expect(tabForRelatedEntity('ReserveHistory')).toBe(DETAIL_TABS.reserves);
    expect(tabForRelatedEntity('ClaimDocument')).toBe(DETAIL_TABS.documents);
    expect(tabForRelatedEntity('ClaimParty')).toBe(DETAIL_TABS.parties);
    expect(tabForRelatedEntity(null)).toBeNull();
  });
});
