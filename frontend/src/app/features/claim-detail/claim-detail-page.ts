import { Clipboard } from '@angular/cdk/clipboard';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { catchError, filter, of, Subject, switchMap, tap, merge, map } from 'rxjs';
import { ClaimsApi } from '../../api/claims-api.service';
import { ClaimCreated, ClaimDetail, ClaimStatus, UpdateClaimStatusResult } from '../../api/models';
import { AuthService } from '../../core/auth/auth.service';
import { userDisplayName } from '../../core/auth/mock-users';
import { NotificationService } from '../../core/notifications/notification.service';
import { claimStatusLabel } from '../../shared/claim-status/claim-status';
import { StatusBadge } from '../../shared/claim-status/status-badge';
import { PageHeader } from '../../shared/page-header/page-header';
import { reserveApprovalLabel, reserveApprovalLevel } from '../../shared/reserve-threshold/reserve-threshold';
import { AuditLogTab } from './audit-log-tab/audit-log-tab';
import { DETAIL_TABS } from './claim-detail-tabs';
import { DocumentsTab } from './documents-tab/documents-tab';
import { OverviewTab } from './overview-tab/overview-tab';
import { PartiesTab } from './parties-tab/parties-tab';
import { ReservesTab } from './reserves-tab/reserves-tab';
import { StatusChangeDialog, StatusChangeDialogData } from './status-change-dialog/status-change-dialog';

@Component({
  selector: 'app-claim-detail-page',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTabsModule,
    MatTooltipModule,
    PageHeader,
    StatusBadge,
    OverviewTab,
    PartiesTab,
    ReservesTab,
    DocumentsTab,
    AuditLogTab,
  ],
  templateUrl: './claim-detail-page.html',
  styleUrl: './claim-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClaimDetailPage {
  private readonly claimsApi = inject(ClaimsApi);
  private readonly dialog = inject(MatDialog);
  private readonly clipboard = inject(Clipboard);
  private readonly notifications = inject(NotificationService);
  private readonly reload$ = new Subject<void>();
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  protected readonly claim = signal<ClaimDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly selectedTab = signal<number>(DETAIL_TABS.overview);
  protected readonly created = signal<ClaimCreated | null>(
    (inject(Router).currentNavigation()?.extras.state as { created?: ClaimCreated } | undefined)?.created ?? null,
  );

  protected readonly statusLabel = claimStatusLabel;
  protected readonly userName = userDisplayName;
  protected readonly tabs = DETAIL_TABS;
  protected readonly statusOptions = computed(() =>
    (this.claim()?.validNextStatuses ?? []).map((status) => {
      const allowed = status !== 'Reopened' || this.auth.canApproveReserves();
      return { status, label: claimStatusLabel(status), allowed, tooltip: allowed ? '' : 'Requires the Supervisor or Manager role' };
    }),
  );
  protected readonly breadcrumbs = computed(() => [{ label: 'Claims', link: '/claims' }, { label: this.claim()?.claimNumber ?? 'Claim' }]);

  constructor() {
    merge(toObservable(this.id).pipe(tap(() => this.claim.set(null))), this.reload$.pipe(map(() => this.id())))
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.notFound.set(false);
        }),
        switchMap((id) =>
          this.claimsApi.get(id).pipe(
            catchError((error: unknown) => {
              this.notFound.set(error instanceof HttpErrorResponse && error.status === 404);
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((claim) => {
        if (claim || !this.claim()) {
          this.claim.set(claim);
        }
        this.loading.set(false);
      });
  }

  protected reload(): void {
    this.reload$.next();
  }

  protected copyClaimNumber(claimNumber: string): void {
    if (this.clipboard.copy(claimNumber)) {
      this.notifications.success(`Copied ${claimNumber} to the clipboard.`);
    }
  }

  protected changeStatus(claim: ClaimDetail, targetStatus: ClaimStatus): void {
    this.dialog
      .open<StatusChangeDialog, StatusChangeDialogData, UpdateClaimStatusResult>(StatusChangeDialog, { data: { claim, targetStatus }, autoFocus: 'dialog' })
      .afterClosed()
      .pipe(filter((result) => !!result))
      .subscribe(() => this.reload());
  }

  protected initialReserveNote(created: ClaimCreated): string | null {
    const transaction = created.initialReserve?.transaction;
    if (!transaction) {
      return null;
    }

    if (transaction.approvalStatus === 'AutoApproved') {
      return `The initial ${transaction.component} reserve was auto-approved and queued for GL posting.`;
    }

    const level = reserveApprovalLevel(transaction.amount);
    return `The initial ${transaction.component} reserve is pending approval${level ? ` (${reserveApprovalLabel(level).replace('⚠ ', '').toLowerCase()})` : ''}.`;
  }
}
