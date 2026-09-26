import { HttpContextToken, HttpErrorResponse } from '@angular/common/http';
import { ApiErrorBody } from '../../api/models';

export const SKIP_ERROR_NOTIFICATION = new HttpContextToken<boolean>(() => false);

export function apiErrorMessages(error: HttpErrorResponse): string[] {
  if (error.status === 0) {
    return ['Cannot reach the server. Check your connection and try again.'];
  }

  const body = error.error as ApiErrorBody | null;

  if (body?.errors) {
    const messages = Object.values(body.errors).flat();
    if (messages.length > 0) {
      return messages;
    }
  }

  if (body?.title) {
    return [body.title];
  }

  if (error.status === 401) {
    return ['You are not signed in.'];
  }

  if (error.status === 403) {
    return ['You do not have permission to perform this action.'];
  }

  return ['An unexpected error occurred. Please try again.'];
}

export function apiErrorMessage(error: HttpErrorResponse): string {
  return apiErrorMessages(error).join(' ');
}
