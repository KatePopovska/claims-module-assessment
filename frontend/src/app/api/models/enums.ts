export const CLAIM_STATUSES = ['Draft', 'Open', 'UnderInvestigation', 'PendingPayment', 'Closed', 'Reopened', 'Withdrawn'] as const;
export type ClaimStatus = (typeof CLAIM_STATUSES)[number];

export const PARTY_ROLES = ['Claimant', 'Insured', 'ThirdParty', 'Witness', 'Attorney'] as const;
export type PartyRole = (typeof PARTY_ROLES)[number];

export const PARTY_TYPES = ['Person', 'Company'] as const;
export type PartyType = (typeof PARTY_TYPES)[number];

export const ASSET_TYPES = ['Vehicle', 'Property', 'Person', 'Equipment', 'Other'] as const;
export type AssetType = (typeof ASSET_TYPES)[number];

export const RESERVE_COMPONENT_TYPES = ['Indemnity', 'Expense', 'ALAE', 'SubrogationRecoverable'] as const;
export type ReserveComponentType = (typeof RESERVE_COMPONENT_TYPES)[number];

export type ReserveComponentStatus = 'Active' | 'Closed';
export type ReserveTransactionType = 'Add' | 'Adjust' | 'Reverse';
export type ReserveApprovalStatus = 'AutoApproved' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled';
export type ReservePostingStatus = 'Pending' | 'Posted' | 'Failed' | 'Cancelled';
export type PolicyStatus = 'Active' | 'Expired' | 'Cancelled';
export type PerilCategory = 'Property' | 'Auto' | 'Liability' | 'Weather' | 'Equipment' | 'Crime' | 'General';
export type Severity = 'Minor' | 'Standard' | 'Critical' | 'Catastrophic';

export type AuditEventType =
  | 'CLAIM_CREATED'
  | 'CLAIM_NOTES_UPDATED'
  | 'STATUS_CHANGED'
  | 'PARTY_ADDED'
  | 'PARTY_REMOVED'
  | 'RESERVE_CREATED'
  | 'RESERVE_AUTO_APPROVED'
  | 'RESERVE_APPROVED'
  | 'RESERVE_REJECTED'
  | 'RESERVE_RETRACTED'
  | 'RESERVE_OVERRIDE_SET'
  | 'GL_POSTING_SIMULATED'
  | 'GL_POSTING_FAILED'
  | 'DOCUMENT_UPLOADED'
  | 'CLAIM_CLOSED'
  | 'CLAIM_REOPENED'
  | 'SLA_BREACH_DETECTED'
  | 'VALIDATION_ISSUE_ADDED';
