import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { ReservesApi } from './reserves-api.service';

describe('ReservesApi', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('posts a GL retry for the transaction', () => {
    TestBed.inject(ReservesApi).retryPosting('claim-1', 'txn-1').subscribe();

    const request = controller.expectOne(`${environment.apiUrl}/claims/claim-1/reserves/txn-1/retry-posting`);
    expect(request.request.method).toBe('POST');
    request.flush({});
  });
});
