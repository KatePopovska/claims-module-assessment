import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable } from 'rxjs';
import { RESERVE_COMPONENT_TYPES, ReserveComponentSummary, ReserveComponentType, ReserveSubmissionResult } from '../../../api/models';
import { ReservesApi } from '../../../api/reserves-api.service';
import { newIdempotencyKey } from '../../../core/http/idempotency';
import { componentLabel } from '../../../shared/format';
import { ReserveThreshold } from '../../../shared/reserve-threshold/reserve-threshold';

export interface ReservePanelData {
  claimId: string;
  claimNumber: string;
  existingComponents: ReserveComponentType[];
  adjust?: ReserveComponentSummary;
}

@Component({
  selector: 'app-reserve-panel',
  imports: [CurrencyPipe, ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, ReserveThreshold],
  templateUrl: './reserve-panel.html',
  styleUrl: './reserve-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReservePanel {
  private readonly reservesApi = inject(ReservesApi);
  private readonly dialogRef = inject(MatDialogRef<ReservePanel, ReserveSubmissionResult>);
  private readonly idempotencyKey = newIdempotencyKey();
  protected readonly data = inject<ReservePanelData>(MAT_DIALOG_DATA);

  protected readonly adjusting = !!this.data.adjust;
  protected readonly availableComponents = RESERVE_COMPONENT_TYPES.filter((c) => !this.data.existingComponents.includes(c));
  protected readonly componentLabel = componentLabel;
  protected readonly submitting = signal(false);

  protected readonly form = new FormGroup({
    component: new FormControl<ReserveComponentType | null>(this.data.adjust?.component ?? this.availableComponents[0] ?? null, [Validators.required]),
    amount: new FormControl<number | null>(null, [Validators.required]),
    changeReason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
  });

  protected readonly amount = toSignal(this.form.controls.amount.valueChanges, { initialValue: null });
  private readonly component = toSignal(this.form.controls.component.valueChanges, { initialValue: this.form.controls.component.value });

  protected readonly amountError = computed(() => {
    const amount = this.amount();
    if (amount === null || amount === undefined) {
      return null;
    }
    if (!/^-?\d+(\.\d{1,4})?$/.test(String(amount))) {
      return 'Use at most 4 decimal places.';
    }
    if (this.adjusting) {
      if (amount === 0) {
        return 'Adjustment amount must not be zero.';
      }
      const component = this.data.adjust!;
      if (component.component !== 'SubrogationRecoverable' && component.currentBalance + amount < 0) {
        return `The ${componentLabel(component.component)} balance cannot go below zero.`;
      }
      return null;
    }

    return this.component() === 'SubrogationRecoverable' ? (amount === 0 ? 'Amount must not be zero.' : null) : amount <= 0 ? 'Reserve amount must be greater than zero.' : null;
  });

  protected readonly projectedBalance = computed(() => {
    const amount = this.amount();
    return this.data.adjust && amount !== null ? this.data.adjust.currentBalance + amount : null;
  });

  protected submit(): void {
    if (this.form.invalid || this.amountError() || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const { component, amount, changeReason } = this.form.getRawValue();
    const reason = changeReason.trim() || null;
    const request$: Observable<ReserveSubmissionResult> = this.data.adjust
      ? this.reservesApi.adjust(this.data.claimId, this.data.adjust.id, { amount: amount!, changeReason: reason }, this.idempotencyKey)
      : this.reservesApi.create(this.data.claimId, { component: component!, amount: amount!, changeReason: reason }, this.idempotencyKey);

    this.submitting.set(true);
    request$.subscribe({
      next: (result) => this.dialogRef.close(result),
      error: () => this.submitting.set(false),
    });
  }
}
