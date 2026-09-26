import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { environment } from '../../environments/environment';
import { CauseOfLossCode, ClaimStatusReference } from './models';

@Injectable({ providedIn: 'root' })
export class ReferenceApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/reference`;

  readonly causeOfLossCodes$: Observable<CauseOfLossCode[]> = this.http
    .get<CauseOfLossCode[]>(`${this.baseUrl}/cause-of-loss-codes`)
    .pipe(shareReplay({ bufferSize: 1, refCount: false }));

  readonly claimStatuses$: Observable<ClaimStatusReference[]> = this.http
    .get<ClaimStatusReference[]>(`${this.baseUrl}/claim-statuses`)
    .pipe(shareReplay({ bufferSize: 1, refCount: false }));
}
