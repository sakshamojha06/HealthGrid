import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AppConfig } from '../config/app-config';

let refreshing = false;
const refreshed$ = new BehaviorSubject<string | null>(null);

/**
 * Attaches the bearer token and transparently refreshes it once on a 401,
 * replaying the original request. A second failure logs the user out.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  const isAuthCall = req.url.startsWith(`${AppConfig.apiBaseUrl}/auth/`);
  const token = auth.accessToken;
  const authed = token && !isAuthCall ? withBearer(req, token) : req;

  return next(authed).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 || isAuthCall || !auth.refreshToken) {
        return throwError(() => err);
      }

      if (refreshing) {
        return refreshed$.pipe(
          filter((t): t is string => t !== null),
          take(1),
          switchMap((t) => next(withBearer(req, t))),
        );
      }

      refreshing = true;
      refreshed$.next(null);
      return auth.refresh().pipe(
        switchMap((tokens) => {
          refreshing = false;
          refreshed$.next(tokens.accessToken);
          return next(withBearer(req, tokens.accessToken));
        }),
        catchError((refreshErr) => {
          refreshing = false;
          auth.logout();
          return throwError(() => refreshErr);
        }),
      );
    }),
  );
};

function withBearer(req: Parameters<HttpInterceptorFn>[0], token: string) {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}
