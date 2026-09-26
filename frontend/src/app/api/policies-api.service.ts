import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PolicyCoverage, PolicySearchResult } from './models';

@Injectable({ providedIn: 'root' })
export class PoliciesApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/policies`;

  search(term: string): Observable<PolicySearchResult[]> {
    return this.http.get<PolicySearchResult[]>(`${this.baseUrl}/search`, { params: new HttpParams().set('q', term) });
  }

  coverage(policyId: string): Observable<PolicyCoverage> {
    return this.http.get<PolicyCoverage>(`${this.baseUrl}/${policyId}/coverage`);
  }
}
