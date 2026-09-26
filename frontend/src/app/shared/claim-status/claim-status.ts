import { ClaimStatus } from '../../api/models';

const STATUS_LABELS: Record<ClaimStatus, string> = {
  Draft: 'Draft',
  Open: 'Open',
  UnderInvestigation: 'Under Investigation',
  PendingPayment: 'Pending Payment',
  Closed: 'Closed',
  Reopened: 'Reopened',
  Withdrawn: 'Withdrawn',
};

export function claimStatusLabel(status: ClaimStatus): string {
  return STATUS_LABELS[status] ?? status;
}
