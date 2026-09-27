import { AuditEventType, PartyRole, ReserveComponentType } from '../api/models';

export function fileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const units = ['KB', 'MB', 'GB'];
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }

  return `${value.toFixed(value < 10 ? 1 : 0)} ${units[unit]}`;
}

export function componentLabel(component: ReserveComponentType): string {
  return component === 'SubrogationRecoverable' ? 'Subrogation Recoverable' : component;
}

export function partyRoleLabel(role: PartyRole): string {
  return role === 'ThirdParty' ? 'Third Party' : role;
}

export function eventTypeLabel(eventType: AuditEventType): string {
  return eventType
    .toLowerCase()
    .split('_')
    .map((word) => (word === 'gl' || word === 'sla' ? word.toUpperCase() : word[0].toUpperCase() + word.slice(1)))
    .join(' ');
}

export function partyDisplayName(party: { partyType: string; firstName: string | null; lastName: string | null; companyName: string | null }): string {
  return party.partyType === 'Company' ? (party.companyName ?? '') : `${party.firstName ?? ''} ${party.lastName ?? ''}`.trim();
}
