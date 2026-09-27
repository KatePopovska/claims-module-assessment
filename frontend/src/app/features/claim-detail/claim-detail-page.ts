import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { ClaimCreated } from '../../api/models';
import { PageHeader } from '../../shared/page-header/page-header';
import { reserveApprovalLabel, reserveApprovalLevel } from '../../shared/reserve-threshold/reserve-threshold';

@Component({
  selector: 'app-claim-detail-page',
  imports: [PageHeader, MatButtonModule, MatIconModule],
  templateUrl: './claim-detail-page.html',
  styleUrl: './claim-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClaimDetailPage {
  readonly id = input.required<string>();

  protected readonly created = signal<ClaimCreated | null>(
    (inject(Router).currentNavigation()?.extras.state as { created?: ClaimCreated } | undefined)?.created ?? null,
  );

  protected initialReserveNote(created: ClaimCreated): string | null {
    const transaction = created.initialReserve?.transaction;
    if (!transaction) {
      return null;
    }

    if (transaction.approvalStatus === 'AutoApproved') {
      return `Initial ${transaction.component} reserve was auto-approved and queued for GL posting.`;
    }

    const level = reserveApprovalLevel(transaction.amount);
    return `Initial ${transaction.component} reserve is pending approval${level ? ` (${reserveApprovalLabel(level).replace('⚠ ', '').toLowerCase()})` : ''}.`;
  }
}
