import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { idempotencyHeaders } from '../core/http/idempotency';
import { AdjustReserveRequest, ClaimReserves, CreateReserveRequest, ReserveSubmissionResult, ReserveTransaction } from './models';

@Injectable({ providedIn: 'root' })
export class ReservesApi {
  private readonly http = inject(HttpClient);

  private url(claimId: string, path = ''): string {
    return `${environment.apiUrl}/claims/${claimId}/${path}`;
  }

  list(claimId: string): Observable<ClaimReserves> {
    return this.http.get<ClaimReserves>(this.url(claimId, 'reserves'));
  }

  create(claimId: string, request: CreateReserveRequest, idempotencyKey?: string): Observable<ReserveSubmissionResult> {
    return this.http.post<ReserveSubmissionResult>(this.url(claimId, 'reserves'), request, { headers: idempotencyHeaders(idempotencyKey) });
  }

  adjust(claimId: string, reserveComponentId: string, request: AdjustReserveRequest, idempotencyKey?: string): Observable<ReserveSubmissionResult> {
    return this.http.put<ReserveSubmissionResult>(this.url(claimId, `reserves/${reserveComponentId}`), request, { headers: idempotencyHeaders(idempotencyKey) });
  }

  approve(claimId: string, transactionId: string): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(this.url(claimId, `reserves/${transactionId}/approve`), {});
  }

  reject(claimId: string, transactionId: string, rejectionReason: string): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(this.url(claimId, `reserves/${transactionId}/reject`), { rejectionReason });
  }

  retract(claimId: string, transactionId: string): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(this.url(claimId, `reserves/${transactionId}/retract`), {});
  }

  setLimitOverride(claimId: string, reason: string): Observable<void> {
    return this.http.put<void>(this.url(claimId, 'reserve-limit-override'), { reason });
  }
}
