import { componentLabel, eventTypeLabel, fileSize, partyRoleLabel } from './format';

describe('format helpers', () => {
  it('formats file sizes', () => {
    expect(fileSize(512)).toBe('512 B');
    expect(fileSize(1536)).toBe('1.5 KB');
    expect(fileSize(12 * 1024 * 1024)).toBe('12 MB');
  });

  it('turns audit event types into readable labels', () => {
    expect(eventTypeLabel('RESERVE_AUTO_APPROVED')).toBe('Reserve Auto Approved');
    expect(eventTypeLabel('GL_POSTING_SIMULATED')).toBe('GL Posting Simulated');
    expect(eventTypeLabel('SLA_BREACH_DETECTED')).toBe('SLA Breach Detected');
  });

  it('spells out compound enum values', () => {
    expect(componentLabel('SubrogationRecoverable')).toBe('Subrogation Recoverable');
    expect(partyRoleLabel('ThirdParty')).toBe('Third Party');
  });
});
