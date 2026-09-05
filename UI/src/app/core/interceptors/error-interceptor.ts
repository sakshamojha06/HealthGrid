import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';

/**
 * Surfaces API errors as a toast so every feature does not have to.
 * 401 is handled by the auth interceptor and stays silent here.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const snack = inject(MatSnackBar);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401) {
        snack.open(describe(err), 'Dismiss', { duration: 6000, panelClass: 'hg-snack-error' });
      }
      return throwError(() => err);
    }),
  );
};

function describe(err: HttpErrorResponse): string {
  if (err.status === 0) {
    return 'Cannot reach the server. Is the API running?';
  }
  const problem = err.error;
  if (problem?.detail) {
    return problem.detail;
  }
  if (problem?.title) {
    return problem.title;
  }
  if (typeof problem === 'string' && problem.trim()) {
    return problem;
  }
  return `Request failed (${err.status})`;
}
