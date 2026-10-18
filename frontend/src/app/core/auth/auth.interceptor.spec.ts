import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthResponse } from '../models/api.models';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const session = (accessToken: string, refreshToken: string): AuthResponse => ({
  accessToken,
  refreshToken,
  accessTokenExpiresAt: new Date().toISOString(),
  refreshTokenExpiresAt: new Date().toISOString(),
  user: { id: 2, fullName: 'Aarav Sharma', email: 'aarav@store.local', role: 'Customer' },
});

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.setItem('store.session', JSON.stringify(session('old-access', 'refresh-1')));

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => {
    backend.verify();
    localStorage.clear();
  });

  it('attaches the access token', () => {
    http.get('/api/orders/my').subscribe();

    const req = backend.expectOne('/api/orders/my');
    expect(req.request.headers.get('Authorization')).toBe('Bearer old-access');
    req.flush({});
  });

  it('refreshes on 401 and retries the original request once with the new token', () => {
    let body: unknown;
    http.get('/api/orders/my').subscribe((b) => (body = b));

    backend.expectOne('/api/orders/my').flush(null, { status: 401, statusText: 'Unauthorized' });

    const refresh = backend.expectOne('/api/auth/refresh');
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    expect(refresh.request.headers.has('Authorization')).toBeFalse();
    refresh.flush(session('new-access', 'refresh-2'));

    const retry = backend.expectOne('/api/orders/my');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-access');
    retry.flush({ ok: true });

    expect(body).toEqual({ ok: true });
    expect(auth.accessToken()).toBe('new-access');
  });

  it('does not retry a second time when the retried request is also 401', () => {
    let status = 0;
    http.get('/api/orders/my').subscribe({ error: (e) => (status = e.status) });

    backend.expectOne('/api/orders/my').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/auth/refresh').flush(session('new-access', 'refresh-2'));
    backend.expectOne('/api/orders/my').flush(null, { status: 401, statusText: 'Unauthorized' });

    // No further /refresh or retry was issued (backend.verify() in afterEach checks this).
    expect(status).toBe(401);
  });

  it('shares one refresh call between parallel 401s', () => {
    http.get('/api/orders/my').subscribe();
    http.get('/api/orders/7').subscribe();

    backend.expectOne('/api/orders/my').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/orders/7').flush(null, { status: 401, statusText: 'Unauthorized' });

    backend.expectOne('/api/auth/refresh').flush(session('new-access', 'refresh-2'));

    backend.expectOne('/api/orders/my').flush({});
    backend.expectOne('/api/orders/7').flush({});
  });

  it('logs the user out when the refresh token is rejected', () => {
    let status = 0;
    http.get('/api/orders/my').subscribe({ error: (e) => (status = e.status) });

    backend.expectOne('/api/orders/my').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
    expect(auth.isLoggedIn()).toBeFalse();
    expect(localStorage.getItem('store.session')).toBeNull();
  });
});
