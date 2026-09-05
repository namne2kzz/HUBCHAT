import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { ConfigService } from '../core/services/config.service';
import { LoginResponse, TokenPayload } from '../models/auth.model';
import { UserProfile } from '../models/user.model';
import { StorageService } from '../core/services/storage.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http    = inject(HttpClient);
  private readonly storage = inject(StorageService);
  private readonly config  = inject(ConfigService);

  readonly isAuthenticated = signal<boolean>(this.storage.has(StorageKeys.accessToken));
  readonly currentUser     = signal<UserProfile | null>(this.storage.get<UserProfile>(StorageKeys.userProfile));

  /**
   * Called by AppComponent after DASHBOARD SSO redirect delivers tokens via URL params.
   * Decodes the JWT payload to build a UserProfile without a network call.
   * @param accessToken Raw JWT access token.
   * @param refreshToken Refresh token.
   */
  applyTokens(accessToken: string, refreshToken: string): void {
    // setString, NOT set: tokens are read back with getString(), so JSON.stringify would
    // persist them wrapped in quotes and every request would send `Bearer "eyJ..."` → 401.
    this.storage.setString(StorageKeys.accessToken, accessToken);
    this.storage.setString(StorageKeys.refreshToken, refreshToken);
    const profile = this.decodeProfile(accessToken);
    if (profile) {
      this.storage.set(StorageKeys.userProfile, profile);
      this.currentUser.set(profile);
    }
    this.isAuthenticated.set(true);
  }

  /** Exchanges refresh token for a new access token. @returns Observable of LoginResponse. */
  refresh(): Observable<LoginResponse> {
    const refreshToken = this.storage.getString(StorageKeys.refreshToken) ?? '';
    return this.http
      .post<LoginResponse>(`${this.config.apiBaseUrl}/auth/refresh`, { refreshToken })
      .pipe(tap(res => this.applyTokens(res.accessToken, res.refreshToken)));
  }

  /**
   * Single logout: clears the HUB session, then hops to DASHBOARD with ?logout=1 to clear
   * its session too, and asks DASHBOARD to bounce back to HUB's /signed-out landing page.
   * (The two apps are separate origins and cannot share localStorage directly.)
   */
  logout(): void {
    this.clearSession();
    const back = encodeURIComponent(`${window.location.origin}/signed-out`);
    window.location.href = `${this.config.dashboardUrl}/login?logout=1&returnUrl=${back}`;
  }

  /** Clears local session state without redirecting. */
  clearSession(): void {
    this.storage.remove(StorageKeys.accessToken);
    this.storage.remove(StorageKeys.refreshToken);
    this.storage.remove(StorageKeys.userProfile);
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
  }

  /** Starts the SSO sign-in flow: redirect to DASHBOARD login, return to the given HUB path. */
  signIn(returnPath = '/channels'): void {
    const back = encodeURIComponent(`${window.location.origin}${returnPath}`);
    window.location.href = `${this.config.dashboardUrl}/login?returnUrl=${back}`;
  }

  /** @returns The stored JWT access token, or null. */
  getToken(): string | null {
    return this.storage.getString(StorageKeys.accessToken);
  }

  private decodeProfile(token: string): UserProfile | null {
    try {
      const payload: TokenPayload = JSON.parse(atob(token.split('.')[1]));
      return { id: payload.sub, email: payload.email, fullName: payload.name, avatarUrl: null, avatarClass: 'avatar-accent' };
    } catch { return null; }
  }
}
