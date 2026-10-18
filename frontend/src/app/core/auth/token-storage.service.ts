import { Injectable } from '@angular/core';
import { AuthResponse, User } from '../models/api.models';

export const SESSION_STORAGE_KEY = 'store.session';

export interface StoredSession {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  user: User;
}

/** Keeps the session in localStorage so a page refresh doesn't log the user out. */
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  load(): StoredSession | null {
    try {
      const raw = localStorage.getItem(SESSION_STORAGE_KEY);
      return raw ? (JSON.parse(raw) as StoredSession) : null;
    } catch {
      return null;
    }
  }

  save(response: AuthResponse): StoredSession {
    const session: StoredSession = {
      accessToken: response.accessToken,
      accessTokenExpiresAt: response.accessTokenExpiresAt,
      refreshToken: response.refreshToken,
      user: response.user,
    };
    try {
      localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
    } catch {
      // storage full / disabled - session still lives in memory
    }
    return session;
  }

  clear(): void {
    try {
      localStorage.removeItem(SESSION_STORAGE_KEY);
    } catch {
      // ignore
    }
  }
}
