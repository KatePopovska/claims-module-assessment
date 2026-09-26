import { AuditEventType } from './enums';

export interface AuditLogEntry {
  id: string;
  eventType: AuditEventType;
  description: string;
  oldValue: string | null;
  newValue: string | null;
  relatedEntityId: string | null;
  relatedEntityType: string | null;
  correlationId: string | null;
  createdAt: string;
  createdByUserId: string | null;
}
