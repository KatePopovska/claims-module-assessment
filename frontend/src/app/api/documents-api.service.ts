import { HttpClient, HttpEvent } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ClaimDocument } from './models';

@Injectable({ providedIn: 'root' })
export class DocumentsApi {
  private readonly http = inject(HttpClient);

  private url(claimId: string): string {
    return `${environment.apiUrl}/claims/${claimId}/documents`;
  }

  list(claimId: string): Observable<ClaimDocument[]> {
    return this.http.get<ClaimDocument[]>(this.url(claimId));
  }

  upload(claimId: string, file: File, documentType: string | null, notes: string | null): Observable<HttpEvent<ClaimDocument>> {
    const form = new FormData();
    form.append('file', file, file.name);
    if (documentType) {
      form.append('documentType', documentType);
    }
    if (notes) {
      form.append('notes', notes);
    }

    return this.http.post<ClaimDocument>(this.url(claimId), form, { reportProgress: true, observe: 'events' });
  }
}
