import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const withToken = (req: HttpRequest<unknown>, token: string) =>
  req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

// Login/register/refresh must never trigger a refresh themselves.
const isAuthEndpoint = (url: string) => url.includes('/api/auth/');

/**
 * Attaches the JWT. On 401 it refreshes the token and retries the original
 * request once; if the refresh fails the user is sent to the login page.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (isAuthEndpoint(req.url)) {
    return next(req);
  }

  const auth = inject(AuthService);
  const token = auth.accessToken();
  const request = token ? withToken(req, token) : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      const expired = error instanceof HttpErrorResponse && error.status === 401 && token !== null;
      if (!expired) {
        return throwError(() => error);
      }

      return auth.refreshAccessToken().pipe(
        catchError(() => {
          auth.sessionExpired();
          return throwError(() => error);
        }),
        // The retry goes straight to `next` (not through this interceptor again),
        // so a second 401 is returned to the caller instead of looping.
        switchMap((newToken) => next(withToken(req, newToken))),
      );
    }),
  );
};
