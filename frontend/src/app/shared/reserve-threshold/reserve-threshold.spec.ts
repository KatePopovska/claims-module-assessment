import { TestBed } from '@angular/core/testing';
import { ReserveThreshold, reserveApprovalLabel, reserveApprovalLevel } from './reserve-threshold';

describe('reserveApprovalLevel', () => {
  it.each([
    [10_000, 'auto'],
    [10_000.01, 'supervisor'],
    [100_000, 'supervisor'],
    [100_000.01, 'manager'],
    [-5_000, 'auto'],
    [-250_000, 'manager'],
  ] as const)('%d requires %s', (amount, level) => {
    expect(reserveApprovalLevel(amount)).toBe(level);
  });

  it.each([null, undefined, 0, Number.NaN])('shows nothing for %s', (amount) => {
    expect(reserveApprovalLevel(amount)).toBeNull();
  });

  it('uses the FRS wording', () => {
    expect(reserveApprovalLabel('auto')).toBe('✓ Auto-approved (≤ $10,000)');
    expect(reserveApprovalLabel('supervisor')).toBe('⚠ Supervisor approval required');
    expect(reserveApprovalLabel('manager')).toBe('⚠ Manager approval required');
  });
});

describe('ReserveThreshold', () => {
  it('renders the indicator for the entered amount', async () => {
    const fixture = TestBed.createComponent(ReserveThreshold);
    fixture.componentRef.setInput('amount', 50_000);
    await fixture.whenStable();

    const indicator = (fixture.nativeElement as HTMLElement).querySelector('.indicator')!;
    expect(indicator.textContent).toContain('Supervisor approval required');
    expect(indicator.classList).toContain('supervisor');
  });
});
