import {
  ReserveApprovalStatus,
  ReserveComponentStatus,
  ReserveComponentType,
  ReservePostingStatus,
  ReserveTransactionType,
} from './enums';

export interface ReserveComponentSummary {
  id: string;
  component: ReserveComponentType;
  status: ReserveComponentStatus;
  currentBalance: number;
  pendingAmount: number;
}

export interface ReserveTransaction {
  id: string;
  reserveComponentId: string;
  component: ReserveComponentType;
  transactionType: ReserveTransactionType;
  amount: number;
  previousBalance: number;
  newBalance: number;
  approvalStatus: ReserveApprovalStatus;
  postingStatus: ReservePostingStatus;
  changeSequence: number;
  changeReason: string | null;
  submittedByUserId: string;
  approvedByUserId: string | null;
  approvedAt: string | null;
  rejectedByUserId: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
  createdAt: string;
}

export interface ReserveSubmissionResult {
  transaction: ReserveTransaction;
  warnings: string[];
}

export interface ClaimReserves {
  totalReserves: number;
  reserveLimitOverride: boolean;
  components: ReserveComponentSummary[];
  transactions: ReserveTransaction[];
}

export interface CreateReserveRequest {
  component: ReserveComponentType;
  amount: number;
  changeReason: string | null;
}

export interface AdjustReserveRequest {
  amount: number;
  changeReason: string | null;
}
