import { AuditLogEntry } from './audit';
import { AssetType, ClaimStatus, PartyRole, PartyType, ReserveComponentType, Severity } from './enums';
import { ReserveComponentSummary, ReserveSubmissionResult } from './reserves';

export interface ClaimSummary {
  id: string;
  claimNumber: string;
  policyNumber: string | null;
  clientName: string | null;
  lossDate: string | null;
  causeOfLossCode: string | null;
  causeOfLossName: string | null;
  status: ClaimStatus;
  assignedHandlerId: string | null;
  totalReserves: number;
}

export interface ClaimListQuery {
  status?: ClaimStatus[];
  dateFrom?: string;
  dateTo?: string;
  assignedHandlerId?: string;
  causeOfLossCode?: string;
  policyId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface LossEvent {
  lossDate: string;
  lossDescription: string;
  lossLocation: string | null;
  causeOfLossCode: string;
  causeOfLossName: string | null;
  estimatedLossAmount: number | null;
  reportDate: string;
  policeReportNumber: string | null;
}

export interface ClaimParty {
  id: string;
  partyRole: PartyRole;
  partyType: PartyType;
  firstName: string | null;
  lastName: string | null;
  companyName: string | null;
  email: string | null;
  phone: string | null;
  notes: string | null;
  isActive: boolean;
}

export interface ClaimRiskObject {
  id: string;
  assetType: AssetType;
  assetDescription: string;
  damageDescription: string | null;
  isPrimary: boolean;
  assetReference: string | null;
}

export interface ClaimDocumentSummary {
  id: string;
  documentType: string;
  documentName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string | null;
}

export interface ClaimDetail {
  id: string;
  claimNumber: string;
  status: ClaimStatus;
  validNextStatuses: ClaimStatus[];
  severity: Severity | null;
  policyId: string | null;
  policyNumber: string | null;
  clientName: string | null;
  reportedDate: string;
  assignedHandlerId: string | null;
  closedAt: string | null;
  closureReason: string | null;
  notes: string | null;
  lossEvent: LossEvent | null;
  parties: ClaimParty[];
  riskObjects: ClaimRiskObject[];
  reserves: { totalReserves: number; components: ReserveComponentSummary[] };
  documents: ClaimDocumentSummary[];
  recentAuditEntries: AuditLogEntry[];
}

export interface CreateClaimParty {
  partyRole: PartyRole;
  partyType: PartyType;
  firstName: string | null;
  lastName: string | null;
  companyName: string | null;
  email: string | null;
  phone: string | null;
}

export interface CreateClaimRiskObject {
  assetType: AssetType;
  assetDescription: string;
  damageDescription: string | null;
  isPrimary: boolean;
  assetReference: string | null;
}

export interface CreateClaimRequest {
  policyId: string | null;
  lossDate: string;
  lossDescription: string;
  lossLocation: string | null;
  causeOfLossCode: string;
  estimatedLossAmount: number | null;
  policeReportNumber: string | null;
  parties: CreateClaimParty[];
  riskObjects: CreateClaimRiskObject[];
  initialReserve: { component: ReserveComponentType; amount: number; changeReason: string | null } | null;
}

export interface ClaimCreated {
  id: string;
  claimNumber: string;
  status: ClaimStatus;
  reportedDate: string;
  initialReserve: ReserveSubmissionResult | null;
}

export interface UpdateClaimStatusRequest {
  targetStatus: ClaimStatus;
  reason: string | null;
  acknowledgeWarnings: boolean;
}

export interface UpdateClaimStatusResult {
  id: string;
  status: ClaimStatus;
}

export interface AddClaimPartyRequest extends CreateClaimParty {
  notes: string | null;
}
