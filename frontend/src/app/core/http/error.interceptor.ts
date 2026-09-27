import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../notifications/notification.service';
import { apiErrorMessage, SKIP_ERROR_NOTIFICATION } from './api-error';

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && !request.context.get(SKIP_ERROR_NOTIFICATION)) {
        notifications.error(apiErrorMessage(error));
      }

      return throwError(() => error);
    }),
  );
};
