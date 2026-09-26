import { HttpHeaders } from '@angular/common/http';

export const IDEMPOTENCY_HEADER = 'Idempotency-Key';

export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

export function idempotencyHeaders(key: string | undefined): HttpHeaders | undefined {
  return key ? new HttpHeaders({ [IDEMPOTENCY_HEADER]: key }) : undefined;
}
