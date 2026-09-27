import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('adds the current user as a Bearer token to API requests', () => {
    http.get(`${environment.apiUrl}/claims`).subscribe();

    const request = controller.expectOne(`${environment.apiUrl}/claims`);
    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${TestBed.inject(AuthService).token()}`);
    request.flush({});
  });

  it('uses the switched user for later requests', () => {
    const auth = TestBed.inject(AuthService);
    auth.switchUser(auth.users[2].id);

    http.get(`${environment.apiUrl}/claims`).subscribe();

    const token = controller.expectOne(`${environment.apiUrl}/claims`).request.headers.get('Authorization')!.replace('Bearer ', '');
    expect(JSON.parse(atob(token)).role).toBe('manager');
  });

  it('does not send the token to other hosts', () => {
    http.get('https://example.com/data').subscribe();

    const request = controller.expectOne('https://example.com/data');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });
});
