import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, OnInit, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { ClaimsApi } from '../../../api/claims-api.service';
import { AuditEventType, AuditLogEntry, PagedResult } from '../../../api/models';
import { userDisplayName } from '../../../core/auth/mock-users';
import { eventTypeLabel } from '../../../shared/format';
import { tabForRelatedEntity } from '../claim-detail-tabs';

const EVENT_TONES: Partial<Record<AuditEventType, string>> = {
  CLAIM_CREATED: 'info',
  STATUS_CHANGED: 'info',
  CLAIM_CLOSED: 'success',
  CLAIM_REOPENED: 'warning',
  RESERVE_AUTO_APPROVED: 'success',
  RESERVE_APPROVED: 'success',
  GL_POSTING_SIMULATED: 'success',
  RESERVE_REJECTED: 'danger',
  GL_POSTING_FAILED: 'danger',
  SLA_BREACH_DETECTED: 'danger',
  RESERVE_OVERRIDE_SET: 'warning',
  RESERVE_RETRACTED: 'warning',
};

@Component({
  selector: 'app-audit-log-tab',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatPaginatorModule, MatProgressBarModule, MatTableModule],
  templateUrl: './audit-log-tab.html',
  styleUrl: './audit-log-tab.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditLogTab implements OnInit {
  private readonly claimsApi = inject(ClaimsApi);

  readonly claimId = input.required<string>();
  readonly openRelated = output<number>();

  protected readonly page = signal<PagedResult<AuditLogEntry> | null>(null);
  protected readonly loading = signal(true);
  protected readonly columns = ['createdAt', 'eventType', 'description', 'user', 'related'];
  protected readonly eventLabel = eventTypeLabel;
  protected readonly userName = userDisplayName;
  protected readonly relatedTab = tabForRelatedEntity;
  protected readonly tone = (eventType: AuditEventType) => EVENT_TONES[eventType] ?? 'neutral';

  ngOnInit(): void {
    this.load(1, 20);
  }

  protected onPage(event: PageEvent): void {
    this.load(event.pageIndex + 1, event.pageSize);
  }

  private load(page: number, pageSize: number): void {
    this.loading.set(true);
    this.claimsApi.auditLog(this.claimId(), page, pageSize).subscribe({
      next: (result) => {
        this.page.set(result);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
