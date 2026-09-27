import { HttpClient, HttpContext, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NotificationService } from '../notifications/notification.service';
import { apiErrorMessages, SKIP_ERROR_NOTIFICATION } from './api-error';
import { errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  const notifications = { error: vi.fn(), success: vi.fn() };

  beforeEach(() => {
    notifications.error.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: NotificationService, useValue: notifications },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('shows validation errors in a snackbar and still rethrows', () => {
    let received: unknown;
    http.post('/api/claims', {}).subscribe({ error: (error) => (received = error) });

    controller
      .expectOne('/api/claims')
      .flush({ errors: { LossDate: ['Loss date cannot be in the future.'] } }, { status: 422, statusText: 'Unprocessable Entity' });

    expect(notifications.error).toHaveBeenCalledWith('Loss date cannot be in the future.');
    expect(received).toBeInstanceOf(HttpErrorResponse);
  });

  it('stays silent when the request opts out', () => {
    http.put('/api/claims/1/status', {}, { context: new HttpContext().set(SKIP_ERROR_NOTIFICATION, true) }).subscribe({ error: () => undefined });

    controller.expectOne('/api/claims/1/status').flush({ errors: { Reason: ['Required.'] } }, { status: 422, statusText: 'Unprocessable Entity' });

    expect(notifications.error).not.toHaveBeenCalled();
  });
});

describe('apiErrorMessages', () => {
  const error = (status: number, body: unknown) => new HttpErrorResponse({ status, error: body });

  it('flattens all field errors', () => {
    expect(apiErrorMessages(error(422, { errors: { A: ['one', 'two'], B: ['three'] } }))).toEqual(['one', 'two', 'three']);
  });

  it('uses the title when there are no field errors', () => {
    expect(apiErrorMessages(error(404, { title: 'Claim was not found.' }))).toEqual(['Claim was not found.']);
  });

  it('explains network failures', () => {
    expect(apiErrorMessages(error(0, null))[0]).toContain('Cannot reach the server');
  });

  it('falls back to a generic message', () => {
    expect(apiErrorMessages(error(500, null))).toEqual(['An unexpected error occurred. Please try again.']);
  });
});
