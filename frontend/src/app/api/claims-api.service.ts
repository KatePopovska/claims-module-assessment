import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { SKIP_ERROR_NOTIFICATION } from '../core/http/api-error';
import { idempotencyHeaders } from '../core/http/idempotency';
import {
  AddClaimPartyRequest,
  AuditLogEntry,
  ClaimCreated,
  ClaimDetail,
  ClaimListQuery,
  ClaimParty,
  ClaimSummary,
  CreateClaimRequest,
  PagedResult,
  UpdateClaimStatusRequest,
  UpdateClaimStatusResult,
} from './models';

@Injectable({ providedIn: 'root' })
export class ClaimsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/claims`;

  list(query: ClaimListQuery): Observable<PagedResult<ClaimSummary>> {
    let params = new HttpParams();
    for (const status of query.status ?? []) {
      params = params.append('status', status);
    }
    for (const key of ['dateFrom', 'dateTo', 'assignedHandlerId', 'causeOfLossCode', 'policyId', 'search', 'page', 'pageSize'] as const) {
      const value = query[key];
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return this.http.get<PagedResult<ClaimSummary>>(this.baseUrl, { params });
  }

  get(claimId: string): Observable<ClaimDetail> {
    return this.http.get<ClaimDetail>(`${this.baseUrl}/${claimId}`);
  }

  create(request: CreateClaimRequest, idempotencyKey?: string): Observable<ClaimCreated> {
    return this.http.post<ClaimCreated>(this.baseUrl, request, { headers: idempotencyHeaders(idempotencyKey) });
  }

  updateStatus(claimId: string, request: UpdateClaimStatusRequest, options?: { handleErrorsLocally?: boolean }): Observable<UpdateClaimStatusResult> {
    return this.http.put<UpdateClaimStatusResult>(`${this.baseUrl}/${claimId}/status`, request, {
      context: new HttpContext().set(SKIP_ERROR_NOTIFICATION, options?.handleErrorsLocally ?? false),
    });
  }

  updateNotes(claimId: string, notes: string | null): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${claimId}/notes`, { notes });
  }

  auditLog(claimId: string, page: number, pageSize: number): Observable<PagedResult<AuditLogEntry>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<AuditLogEntry>>(`${this.baseUrl}/${claimId}/audit`, { params });
  }

  addParty(claimId: string, request: AddClaimPartyRequest): Observable<ClaimParty> {
    return this.http.post<ClaimParty>(`${this.baseUrl}/${claimId}/parties`, request);
  }

  removeParty(claimId: string, partyId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${claimId}/parties/${partyId}`);
  }
}
