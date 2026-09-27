import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { filter, Observable, switchMap } from 'rxjs';
import {
  ClaimDetail,
  ClaimReserves,
  RESERVE_COMPONENT_TYPES,
  ReserveApprovalStatus,
  ReserveComponentSummary,
  ReserveSubmissionResult,
  ReserveTransaction,
} from '../../../api/models';
import { ReservesApi } from '../../../api/reserves-api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { userDisplayName } from '../../../core/auth/mock-users';
import { NotificationService } from '../../../core/notifications/notification.service';
import { componentLabel } from '../../../shared/format';
import { PromptDialog, PromptDialogData } from '../../../shared/prompt-dialog/prompt-dialog';
import { SUPERVISOR_APPROVAL_LIMIT } from '../../../shared/reserve-threshold/reserve-threshold';
import { ReservePanel, ReservePanelData } from './reserve-panel';

const APPROVAL_LABELS: Record<ReserveApprovalStatus, string> = {
  AutoApproved: 'Auto-approved',
  PendingApproval: 'Pending approval',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Cancelled: 'Retracted',
};

@Component({
  selector: 'app-reserves-tab',
  imports: [CurrencyPipe, DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule, MatTableModule, MatTooltipModule],
  templateUrl: './reserves-tab.html',
  styleUrl: './reserves-tab.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReservesTab implements OnInit {
  private readonly reservesApi = inject(ReservesApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  protected readonly auth = inject(AuthService);

  readonly claim = input.required<ClaimDetail>();
  readonly changed = output<void>();

  protected readonly reserves = signal<ClaimReserves | null>(null);
  protected readonly loading = signal(true);
  protected readonly busyTransactionId = signal<string | null>(null);
  protected readonly hasPolicy = computed(() => !!this.claim().policyId);

  protected readonly transactionRows = computed(() => {
    const userId = this.auth.currentUser().id.toLowerCase();
    const canDecide = this.auth.canApproveReserves();
    const supervisor = this.auth.role() === 'supervisor';

    return (this.reserves()?.transactions ?? []).map((transaction) => {
      const pending = transaction.approvalStatus === 'PendingApproval';
      const ownSubmission = transaction.submittedByUserId.toLowerCase() === userId;
      const authorityBlock = supervisor && Math.abs(transaction.amount) > SUPERVISOR_APPROVAL_LIMIT ? 'Requires Manager approval (over $100,000).' : null;

      return {
        transaction,
        componentLabel: componentLabel(transaction.component),
        approvalLabel: APPROVAL_LABELS[transaction.approvalStatus],
        showPosting: transaction.approvalStatus === 'AutoApproved' || transaction.approvalStatus === 'Approved',
        submittedBy: userDisplayName(transaction.submittedByUserId),
        decidedBy: transaction.approvedByUserId
          ? userDisplayName(transaction.approvedByUserId)
          : transaction.rejectedByUserId
            ? `Rejected by ${userDisplayName(transaction.rejectedByUserId)}`
            : '—',
        showDecision: pending && canDecide,
        approveBlockedReason: ownSubmission ? 'Self-approval is not permitted.' : authorityBlock,
        rejectBlockedReason: authorityBlock,
        canRetract: pending && ownSubmission,
      };
    });
  });

  protected readonly componentCards = computed(() =>
    (this.reserves()?.components ?? []).map((component) => ({
      component,
      label: componentLabel(component.component),
      pending: component.pendingAmount !== 0,
      canAdjust: this.hasPolicy() && component.pendingAmount === 0 && component.status !== 'Closed',
    })),
  );

  protected readonly canAddReserve = computed(() => this.hasPolicy() && (this.reserves()?.components.length ?? RESERVE_COMPONENT_TYPES.length) < RESERVE_COMPONENT_TYPES.length);
  protected readonly columns = ['createdAt', 'transactionType', 'component', 'amount', 'approvalStatus', 'postingStatus', 'submittedBy', 'approvedBy', 'actions'];

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.reservesApi.list(this.claim().id).subscribe({
      next: (reserves) => {
        this.reserves.set(reserves);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected openPanel(adjust?: ReserveComponentSummary): void {
    const reserves = this.reserves();
    const claim = this.claim();
    this.dialog
      .open<ReservePanel, ReservePanelData, ReserveSubmissionResult>(ReservePanel, {
        data: { claimId: claim.id, claimNumber: claim.claimNumber, existingComponents: reserves?.components.map((c) => c.component) ?? [], adjust },
        position: { right: '0', top: '0' },
        height: '100vh',
        width: '440px',
        maxHeight: '100vh',
        panelClass: 'app-side-panel',
        autoFocus: 'first-tabbable',
      })
      .afterClosed()
      .pipe(filter((result): result is ReserveSubmissionResult => !!result))
      .subscribe((result) => {
        const { transaction, warnings } = result;
        const outcome = transaction.approvalStatus === 'AutoApproved' ? 'auto-approved and queued for GL posting' : 'submitted for approval';
        this.notifications.success(`${componentLabel(transaction.component)} reserve ${outcome}.${warnings.length ? ' ' + warnings.join(' ') : ''}`);
        this.refresh();
      });
  }

  protected approve(transaction: ReserveTransaction): void {
    this.runAction(transaction, this.reservesApi.approve(this.claim().id, transaction.id), 'Reserve approved and queued for GL posting.');
  }

  protected retract(transaction: ReserveTransaction): void {
    this.runAction(transaction, this.reservesApi.retract(this.claim().id, transaction.id), 'Reserve retracted.');
  }

  protected reject(transaction: ReserveTransaction): void {
    this.dialog
      .open<PromptDialog, PromptDialogData, string>(PromptDialog, {
        data: { title: 'Reject reserve', label: 'Rejection reason', confirmLabel: 'Reject reserve', maxLength: 1000 },
      })
      .afterClosed()
      .pipe(filter((reason): reason is string => !!reason))
      .subscribe((reason) => this.runAction(transaction, this.reservesApi.reject(this.claim().id, transaction.id, reason), 'Reserve rejected.'));
  }

  protected setLimitOverride(): void {
    this.dialog
      .open<PromptDialog, PromptDialogData, string>(PromptDialog, {
        data: {
          title: 'Set $10M reserve limit override',
          message: 'Allows approved reserves on this claim to exceed $10,000,000.',
          label: 'Override reason',
          confirmLabel: 'Set override',
          maxLength: 500,
        },
      })
      .afterClosed()
      .pipe(
        filter((reason): reason is string => !!reason),
        switchMap((reason) => this.reservesApi.setLimitOverride(this.claim().id, reason)),
      )
      .subscribe(() => {
        this.notifications.success('Reserve limit override set.');
        this.refresh();
      });
  }

  private runAction(transaction: ReserveTransaction, request$: Observable<unknown>, successMessage: string): void {
    this.busyTransactionId.set(transaction.id);
    request$.subscribe({
      next: () => {
        this.busyTransactionId.set(null);
        this.notifications.success(successMessage);
        this.refresh();
      },
      error: () => this.busyTransactionId.set(null),
    });
  }

  private refresh(): void {
    this.load();
    this.changed.emit();
  }
}
