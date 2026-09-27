import { ClaimStatus, PerilCategory, PolicyStatus } from './enums';

export interface CauseOfLossCode {
  code: string;
  name: string;
  perilCategory: PerilCategory;
  sortOrder: number;
}

export interface ClaimStatusReference {
  status: ClaimStatus;
  validNextStatuses: ClaimStatus[];
}

export interface PolicySearchResult {
  id: string;
  policyNumber: string;
  clientName: string;
  effectiveDate: string;
  expirationDate: string;
  status: PolicyStatus;
  coverageTypes: string;
}

export interface PolicyCoverage {
  policyId: string;
  policyNumber: string;
  status: PolicyStatus;
  effectiveDate: string;
  expirationDate: string;
  coverageTypes: string[];
}

export interface ClaimDocument {
  id: string;
  documentType: string;
  documentName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string | null;
  notes: string | null;
  downloadUrl: string;
  downloadUrlExpiresAt: string | null;
}
