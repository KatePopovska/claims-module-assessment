import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatStepper, MatStepperModule } from '@angular/material/stepper';
import { Router, RouterLink } from '@angular/router';
import { filter, finalize, merge, of, startWith, switchMap } from 'rxjs';
import { ClaimsApi } from '../../api/claims-api.service';
import { ApiErrorBody, ClaimCreated } from '../../api/models';
import { newIdempotencyKey } from '../../core/http/idempotency';
import { NotificationService } from '../../core/notifications/notification.service';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog/confirm-dialog';
import { PageHeader } from '../../shared/page-header/page-header';
import { createFnolForm, errorsByStep, intakeWarnings, selectedPolicy, toCreateClaimRequest } from './fnol-form';
import { PartiesStep } from './parties-step/parties-step';
import { PolicyLossStep } from './policy-loss-step/policy-loss-step';
import { ReviewStep } from './review-step/review-step';

export interface ClaimCreatedNavigationState {
  created: ClaimCreated;
}

@Component({
  selector: 'app-fnol-page',
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatStepperModule, RouterLink, PageHeader, PolicyLossStep, PartiesStep, ReviewStep],
  templateUrl: './fnol-page.html',
  styleUrl: './fnol-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FnolPage {
  private readonly claimsApi = inject(ClaimsApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly idempotencyKey = newIdempotencyKey();
  private readonly statusVersion = signal(0);
  private readonly stepper = viewChild.required(MatStepper);

  protected readonly form = createFnolForm();
  protected readonly submitting = signal(false);
  protected readonly stepErrors = signal<string[][]>([[], [], []]);

  protected readonly canSubmit = computed(() => {
    this.statusVersion();
    return this.form.policyLoss.valid && this.form.parties.valid && !this.form.reserve.invalid;
  });

  constructor() {
    const destroyRef = inject(DestroyRef);
    const { policyLoss, parties, reserve } = this.form;

    merge(policyLoss.statusChanges, parties.statusChanges, reserve.statusChanges)
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(() => this.statusVersion.update((v) => v + 1));

    reserve.controls.enabled.valueChanges.pipe(takeUntilDestroyed(destroyRef)).subscribe((enabled) => {
      const { component, amount, changeReason } = reserve.controls;
      for (const control of [component, amount, changeReason]) {
        if (enabled) {
          control.enable();
        } else {
          control.disable();
        }
      }
    });

    reserve.controls.component.valueChanges
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(() => reserve.controls.amount.updateValueAndValidity());

    merge(policyLoss.controls.unknownPolicy.valueChanges, policyLoss.controls.policy.valueChanges)
      .pipe(startWith(null), takeUntilDestroyed(destroyRef))
      .subscribe(() => {
        const enabled = reserve.controls.enabled;
        if (selectedPolicy(policyLoss)) {
          enabled.enable({ emitEvent: false });
        } else {
          enabled.setValue(false);
          enabled.disable({ emitEvent: false });
        }
      });
  }

  protected touch(control: AbstractControl): void {
    control.markAllAsTouched();
  }

  protected submit(): void {
    if (!this.canSubmit() || this.submitting()) {
      this.form.policyLoss.markAllAsTouched();
      this.form.parties.markAllAsTouched();
      this.form.reserve.markAllAsTouched();
      return;
    }

    const warnings = intakeWarnings(this.form);
    const confirmed$ =
      warnings.length === 0
        ? of(true)
        : this.dialog
            .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
              width: '520px',
              data: {
                title: 'Create claim with warnings?',
                message: 'The following warnings do not block creation, but will be recorded on the claim:',
                items: warnings,
                confirmLabel: 'Create claim',
                tone: 'warning',
              },
            })
            .afterClosed();

    confirmed$
      .pipe(
        filter((confirmed) => confirmed === true),
        switchMap(() => {
          this.submitting.set(true);
          this.stepErrors.set([[], [], []]);
          return this.claimsApi.create(toCreateClaimRequest(this.form), this.idempotencyKey).pipe(finalize(() => this.submitting.set(false)));
        }),
      )
      .subscribe({
        next: (created) => this.onCreated(created),
        error: (error: unknown) => this.onError(error),
      });
  }

  private onCreated(created: ClaimCreated): void {
    this.notifications.success(`Claim ${created.claimNumber} created.`);
    const state: ClaimCreatedNavigationState = { created };
    this.router.navigate(['/claims', created.id], { state });
  }

  private onError(error: unknown): void {
    if (!(error instanceof HttpErrorResponse) || error.status !== 422) {
      return;
    }

    const body = error.error as ApiErrorBody | null;
    const byStep = errorsByStep(body?.errors ?? {});
    this.stepErrors.set(byStep);

    const firstStepWithErrors = byStep.findIndex((messages) => messages.length > 0);
    if (firstStepWithErrors >= 0) {
      this.stepper().selectedIndex = firstStepWithErrors;
    }
  }
}
