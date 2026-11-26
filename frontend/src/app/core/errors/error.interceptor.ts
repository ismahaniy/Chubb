import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { Schemas } from '../api/api';
import { AuthStore } from '../auth/auth.store';
import { Toast, ToastStore } from './toast.store';

/**
 * Set on background requests (polling) that show their own inline error state: a failure is not toasted, so an
 * outage does not raise a new toast every few seconds. A 401 still ends the session.
 */
export const SILENT_ERRORS = new HttpContextToken<boolean>(() => false);

/**
 * Turns failed API calls into toasts (RFC 7807 ProblemDetails when the body has one) and rethrows,
 * so callers can still react. A 401 on an authenticated request ends the session.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toasts = inject(ToastStore);
  const auth = inject(AuthStore);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        if (error.status === 401 && req.headers.has('Authorization')) {
          auth.logout();
          void router.navigate(['/login']);
          toasts.show({ kind: 'error', title: 'Your session has ended. Please sign in again.' });
        } else if (!req.context.get(SILENT_ERRORS)) {
          toasts.show(describe(error));
        }
      }
      return throwError(() => error);
    }),
  );
};

function describe(error: HttpErrorResponse): Omit<Toast, 'id'> {
  const body: unknown = error.error;
  const problem =
    typeof body === 'object' && body !== null
      ? (body as Schemas['ValidationProblemDetails'])
      : null;

  // Status 0 = no response; 502-504 without a ProblemDetails body = the dev proxy could not reach the API.
  if (error.status === 0 || (problem === null && error.status >= 502 && error.status <= 504)) {
    return {
      kind: 'error',
      title: 'Cannot reach the server',
      detail: 'Check your connection and try again.',
    };
  }

  const fieldErrors = Object.values(problem?.errors ?? {}).flat();
  const detail = [problem?.detail, ...fieldErrors].filter(Boolean).join(' ');
  return {
    kind: 'error',
    title: problem?.title || `Request failed (${error.status})`,
    ...(detail ? { detail } : {}),
  };
}
