import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

export type ReserveApprovalLevel = 'auto' | 'supervisor' | 'manager';

export const AUTO_APPROVAL_LIMIT = 10_000;
export const SUPERVISOR_APPROVAL_LIMIT = 100_000;

export function reserveApprovalLevel(amount: number | null | undefined): ReserveApprovalLevel | null {
  if (amount === null || amount === undefined || Number.isNaN(amount) || amount === 0) {
    return null;
  }

  const absolute = Math.abs(amount);
  if (absolute <= AUTO_APPROVAL_LIMIT) {
    return 'auto';
  }

  return absolute <= SUPERVISOR_APPROVAL_LIMIT ? 'supervisor' : 'manager';
}

const LABELS: Record<ReserveApprovalLevel, string> = {
  auto: '✓ Auto-approved (≤ $10,000)',
  supervisor: '⚠ Supervisor approval required',
  manager: '⚠ Manager approval required',
};

export function reserveApprovalLabel(level: ReserveApprovalLevel): string {
  return LABELS[level];
}

@Component({
  selector: 'app-reserve-threshold',
  imports: [MatIconModule],
  template: `
    @if (level(); as level) {
      <span class="indicator" [class]="'indicator ' + level" role="status">{{ label() }}</span>
    }
  `,
  styles: `
    .indicator {
      display: inline-block;
      padding: 6px 12px;
      border-radius: 8px;
      font-size: 13px;
      font-weight: 600;
    }
    .auto {
      background: #dcfce7;
      color: #166534;
    }
    .supervisor {
      background: #fef3c7;
      color: #92400e;
    }
    .manager {
      background: #fee2e2;
      color: #991b1b;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReserveThreshold {
  readonly amount = input<number | null | undefined>(null);
  protected readonly level = computed(() => reserveApprovalLevel(this.amount()));
  protected readonly label = computed(() => {
    const level = this.level();
    return level ? reserveApprovalLabel(level) : '';
  });
}
