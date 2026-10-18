import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { SESSION_STORAGE_KEY } from './token-storage.service';

const user = { id: 2, fullName: 'Aarav Sharma', email: 'aarav@store.local', role: 'Customer' as const };

function storeSession(accessToken: string, expiresInMs: number) {
  localStorage.setItem(
    SESSION_STORAGE_KEY,
    JSON.stringify({
      accessToken,
      accessTokenExpiresAt: new Date(Date.now() + expiresInMs).toISOString(),
      refreshToken: 'refresh-1',
      user,
    }),
  );
}

describe('AuthService.freshAccessToken (used by SignalR)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => {
    backend?.verify();
    localStorage.clear();
  });

  it('returns the current token while it is still valid', async () => {
    storeSession('valid-token', 10 * 60_000);
    const auth = TestBed.inject(AuthService);
    backend = TestBed.inject(HttpTestingController);

    expect(await auth.freshAccessToken()).toBe('valid-token');
  });

  it('refreshes first when the token is about to expire', async () => {
    storeSession('old-token', 5_000);
    const auth = TestBed.inject(AuthService);
    backend = TestBed.inject(HttpTestingController);

    const pending = auth.freshAccessToken();
    backend.expectOne('/api/auth/refresh').flush({
      accessToken: 'new-token',
      accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
      refreshToken: 'refresh-2',
      refreshTokenExpiresAt: new Date(Date.now() + 7 * 86_400_000).toISOString(),
      user,
    });

    expect(await pending).toBe('new-token');
    expect(JSON.parse(localStorage.getItem(SESSION_STORAGE_KEY)!).refreshToken).toBe('refresh-2');
  });
});
