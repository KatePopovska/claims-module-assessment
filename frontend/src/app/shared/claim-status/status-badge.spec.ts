import { TestBed } from '@angular/core/testing';
import { CLAIM_STATUSES, ClaimStatus } from '../../api/models';
import { claimStatusLabel } from './claim-status';
import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  async function render(status: ClaimStatus): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('status', status);
    await fixture.whenStable();
    return (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('.badge')!;
  }

  it.each(CLAIM_STATUSES)('renders %s with its colour class', async (status) => {
    const badge = await render(status);

    expect(badge.classList).toContain(`status-${status}`);
    expect(badge.textContent?.trim()).toBe(claimStatusLabel(status));
  });

  it('spells out multi-word statuses', () => {
    expect(claimStatusLabel('UnderInvestigation')).toBe('Under Investigation');
    expect(claimStatusLabel('PendingPayment')).toBe('Pending Payment');
  });
});
