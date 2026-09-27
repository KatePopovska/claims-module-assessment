import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ClaimsApi } from '../../../api/claims-api.service';
import { ApiErrorBody, ClaimDetail, ClaimStatus, UpdateClaimStatusResult } from '../../../api/models';
import { apiErrorMessages } from '../../../core/http/api-error';
import { NotificationService } from '../../../core/notifications/notification.service';
import { claimStatusLabel } from '../../../shared/claim-status/claim-status';
import { StatusBadge } from '../../../shared/claim-status/status-badge';

export interface StatusChangeDialogData {
  claim: ClaimDetail;
  targetStatus: ClaimStatus;
}

interface ChecklistItem {
  label: string;
  state: 'pass' | 'fail' | 'warn';
}

export const REASON_MAX_LENGTH = 500;
const MIN_DESCRIPTION_LENGTH = 20;

export function closureChecklist(claim: ClaimDetail): ChecklistItem[] {
  const loss = claim.lossEvent;
  const lossComplete = !!loss && loss.lossDescription.trim().length >= MIN_DESCRIPTION_LENGTH && new Date(loss.lossDate).getTime() <= Date.now();

  return [
    { label: 'No reserve transactions are pending approval', state: claim.reserves.components.some((c) => c.pendingAmount !== 0) ? 'fail' : 'pass' },
    { label: 'Loss event is complete (no critical validation issues)', state: lossComplete ? 'pass' : 'fail' },
    { label: 'At least one active Claimant party', state: claim.parties.some((p) => p.isActive && p.partyRole === 'Claimant') ? 'pass' : 'fail' },
    {
      label: hasOpenReserves(claim) ? 'Open reserve balances remain: a justification note is required' : 'No open reserve balances',
      state: hasOpenReserves(claim) ? 'warn' : 'pass',
    },
  ];
}

export function hasOpenReserves(claim: ClaimDetail): boolean {
  return claim.reserves.components.some((c) => c.currentBalance > 0);
}

export function isReasonRequired(claim: ClaimDetail, target: ClaimStatus): boolean {
  return target === 'Withdrawn' || target === 'Reopened' || (target === 'Closed' && hasOpenReserves(claim));
}

@Component({
  selector: 'app-status-change-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    StatusBadge,
  ],
  templateUrl: './status-change-dialog.html',
  styleUrl: './status-change-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusChangeDialog {
  private readonly claimsApi = inject(ClaimsApi);
  private readonly notifications = inject(NotificationService);
  private readonly dialogRef = inject(MatDialogRef<StatusChangeDialog, UpdateClaimStatusResult>);
  protected readonly data = inject<StatusChangeDialogData>(MAT_DIALOG_DATA);

  protected readonly targetLabel = claimStatusLabel(this.data.targetStatus);
  protected readonly reasonRequired = isReasonRequired(this.data.claim, this.data.targetStatus);
  protected readonly reasonMaxLength = REASON_MAX_LENGTH;
  protected readonly checklist = this.data.targetStatus === 'Closed' ? closureChecklist(this.data.claim) : [];
  protected readonly checklistBlocks = this.checklist.some((item) => item.state === 'fail');

  protected readonly reason = new FormControl('', {
    nonNullable: true,
    validators: this.reasonRequired ? [Validators.required, Validators.maxLength(REASON_MAX_LENGTH)] : [Validators.maxLength(REASON_MAX_LENGTH)],
  });
  protected readonly acknowledge = new FormControl(false, { nonNullable: true });

  protected readonly submitting = signal(false);
  protected readonly serverIssues = signal<string[]>([]);
  protected readonly warningsToAcknowledge = signal<string[]>([]);
  protected readonly needsAcknowledgement = computed(() => this.warningsToAcknowledge().length > 0);

  protected confirm(): void {
    this.reason.setValue(this.reason.value.trim());
    if (this.reason.invalid || this.checklistBlocks || (this.needsAcknowledgement() && !this.acknowledge.value)) {
      this.reason.markAsTouched();
      this.acknowledge.markAsTouched();
      return;
    }

    this.submitting.set(true);
    this.serverIssues.set([]);

    this.claimsApi
      .updateStatus(
        this.data.claim.id,
        { targetStatus: this.data.targetStatus, reason: this.reason.value || null, acknowledgeWarnings: this.acknowledge.value },
        { handleErrorsLocally: true },
      )
      .subscribe({
        next: (result) => {
          this.notifications.success(`Claim ${this.data.claim.claimNumber} is now ${claimStatusLabel(result.status)}.`);
          this.dialogRef.close(result);
        },
        error: (error: unknown) => {
          this.submitting.set(false);
          this.handleError(error);
        },
      });
  }

  private handleError(error: unknown): void {
    if (!(error instanceof HttpErrorResponse)) {
      return;
    }

    const errors = (error.error as ApiErrorBody | null)?.errors;
    if (error.status === 422 && errors) {
      const { Warnings: warnings = [], ...blocking } = errors;
      this.warningsToAcknowledge.set(warnings.map((w) => w.replace(' Acknowledge this warning to open the claim.', '')));
      this.serverIssues.set(Object.values(blocking).flat());
      return;
    }

    const messages = apiErrorMessages(error);
    this.serverIssues.set(messages);
    this.notifications.error(messages.join(' '));
  }
}
