import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ClaimStatus } from '../../api/models';
import { claimStatusLabel } from './claim-status';

@Component({
  selector: 'app-status-badge',
  template: `<span class="badge" [class]="'badge status-' + status()">{{ label() }}</span>`,
  styleUrl: './status-badge.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadge {
  readonly status = input.required<ClaimStatus>();
  protected readonly label = computed(() => claimStatusLabel(this.status()));
}
