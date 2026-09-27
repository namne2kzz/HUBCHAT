import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { ConfigService } from '../core/services/config.service';
import { StorageService } from '../core/services/storage.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';
import { NavigationService } from '../core/services/navigation.service';

/**
 * Covers AuthService: the session state HUB keeps locally, and the SSO hand-offs to DASHBOARD.
 *
 * HUB has no login of its own — DASHBOARD issues the tokens and HUB only stores and validates them.
 * So almost everything here is about storage and redirects rather than about credentials.
 */
describe('AuthService', () => {
  let service: AuthService;
  let storage: StorageService;
  let http: HttpTestingController;
  let navigation: jasmine.SpyObj<NavigationService>;

  const API = 'http://localhost:5000/api/v1';
  const DASHBOARD = 'http://localhost:4200';
  const ORIGIN = 'http://hub.test';

  /** Builds a JWT whose payload decodes to the given claims. Only the payload segment is read. */
  const jwt = (claims: Record<string, unknown>) =>
    `header.${btoa(JSON.stringify(claims))}.signature`;

  beforeEach(() => {
    navigation = jasmine.createSpyObj<NavigationService>('NavigationService', ['goTo', 'currentUrl', 'origin']);
    navigation.origin.and.returnValue(ORIGIN);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: NavigationService, useValue: navigation },
        { provide: ConfigService, useValue: { apiBaseUrl: API, dashboardUrl: DASHBOARD, hubUrl: '' } },
      ],
    });

    localStorage.clear();
    service = TestBed.inject(AuthService);
    storage = TestBed.inject(StorageService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  describe('applyTokens', () => {
    it('stores the access token raw, not JSON-encoded', () => {
      const token = jwt({ sub: 'u1', email: 'a@b.com', name: 'Alice' });

      service.applyTokens(token, 'refresh-token');

      // Regression for a real defect the source still carries a comment about: storing via set()
      // instead of setString() wraps the token in quotes, so every request sent
      // `Bearer "eyJ..."` and came back 401. Reading the raw localStorage value is the only way to
      // see the difference — getString() would hide it.
      expect(localStorage.getItem(StorageKeys.accessToken)).toBe(token);
      expect(localStorage.getItem(StorageKeys.accessToken)).not.toContain('"');
    });

    it('stores the refresh token raw as well', () => {
      service.applyTokens(jwt({ sub: 'u1' }), 'refresh-token');

      expect(localStorage.getItem(StorageKeys.refreshToken)).toBe('refresh-token');
    });

    it('marks the session authenticated', () => {
      expect(service.isAuthenticated()).toBeFalse();

      service.applyTokens(jwt({ sub: 'u1' }), 'r');

      expect(service.isAuthenticated()).toBeTrue();
    });

    it('builds the profile from the token without a network call', () => {
      // The claims are already in the JWT, so asking the API for them again would add a round trip
      // to every page load. http.verify() in afterEach is what proves no request went out.
      service.applyTokens(jwt({ sub: 'u1', email: 'alice@example.com', name: 'Alice' }), 'r');

      const user = service.currentUser();
      expect(user?.id).toBe('u1');
      expect(user?.email).toBe('alice@example.com');
      expect(user?.fullName).toBe('Alice');
    });

    it('still authenticates when the token payload cannot be decoded', () => {
      // A malformed token should not throw during bootstrap. The session is marked authenticated and
      // the first API call will get the real answer — a 401 handled by the error interceptor.
      service.applyTokens('not-a-jwt', 'r');

      expect(service.isAuthenticated()).toBeTrue();
      expect(service.currentUser()).toBeNull();
    });
  });

  describe('getToken', () => {
    it('returns the stored token', () => {
      const token = jwt({ sub: 'u1' });
      service.applyTokens(token, 'r');

      expect(service.getToken()).toBe(token);
    });

    it('returns null when there is no session', () => {
      expect(service.getToken()).toBeNull();
    });
  });

  describe('clearSession', () => {
    it('removes every stored credential and resets the signals', () => {
      service.applyTokens(jwt({ sub: 'u1', name: 'Alice' }), 'r');

      service.clearSession();

      expect(localStorage.getItem(StorageKeys.accessToken)).toBeNull();
      expect(localStorage.getItem(StorageKeys.refreshToken)).toBeNull();
      expect(localStorage.getItem(StorageKeys.userProfile)).toBeNull();
      expect(service.isAuthenticated()).toBeFalse();
      expect(service.currentUser()).toBeNull();
    });

    it('does not redirect', () => {
      // clearSession is the half of logout that other code reuses — the error interceptor clears a
      // stale session without wanting the DASHBOARD round trip.
      service.applyTokens(jwt({ sub: 'u1' }), 'r');
      service.clearSession();

      expect(navigation.goTo).not.toHaveBeenCalled();
    });
  });

  describe('logout', () => {
    it('clears the session before leaving', () => {
      service.applyTokens(jwt({ sub: 'u1' }), 'r');

      service.logout();

      // Order matters: if the redirect fired first and the navigation were cancelled, the person
      // would still be signed in.
      expect(service.isAuthenticated()).toBeFalse();
      expect(localStorage.getItem(StorageKeys.accessToken)).toBeNull();
    });

    it('asks DASHBOARD to end its session too', () => {
      service.logout();

      // Single logout: clearing only HUB's localStorage would leave DASHBOARD signed in, and the next
      // visit would silently sign the person straight back in.
      const [url] = navigation.goTo.calls.mostRecent().args;
      expect(url).toContain(`${DASHBOARD}/login`);
      expect(url).toContain('logout=1');
    });

    it('comes back to the signed-out landing page', () => {
      service.logout();

      const [url] = navigation.goTo.calls.mostRecent().args;
      expect(url).toContain(`returnUrl=${encodeURIComponent(`${ORIGIN}/signed-out`)}`);
    });
  });

  describe('signIn', () => {
    it('sends the person to DASHBOARD login', () => {
      service.signIn();

      const [url] = navigation.goTo.calls.mostRecent().args;
      expect(url).toContain(`${DASHBOARD}/login`);
      expect(url).not.toContain('logout=1');
    });

    it('defaults the return path to the channel list', () => {
      service.signIn();

      expect(navigation.goTo.calls.mostRecent().args[0])
        .toContain(`returnUrl=${encodeURIComponent(`${ORIGIN}/channels`)}`);
    });

    it('preserves a requested return path', () => {
      // A deep link from DASHBOARD ("Open in HUB" on a sprint) has to survive the sign-in round trip.
      service.signIn('/acme/channels');

      expect(navigation.goTo.calls.mostRecent().args[0])
        .toContain(`returnUrl=${encodeURIComponent(`${ORIGIN}/acme/channels`)}`);
    });
  });

  describe('refresh', () => {
    it('posts the stored refresh token and applies what comes back', () => {
      service.applyTokens(jwt({ sub: 'u1' }), 'old-refresh');

      const fresh = jwt({ sub: 'u1', name: 'Alice' });
      service.refresh().subscribe();

      const req = http.expectOne(`${API}/auth/refresh`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ refreshToken: 'old-refresh' });

      req.flush({ accessToken: fresh, refreshToken: 'new-refresh' });

      // Both tokens rotate: keeping the old refresh token would make the next refresh fail once the
      // server invalidates it.
      expect(localStorage.getItem(StorageKeys.accessToken)).toBe(fresh);
      expect(localStorage.getItem(StorageKeys.refreshToken)).toBe('new-refresh');
    });

    it('sends an empty refresh token rather than null when none is stored', () => {
      service.refresh().subscribe({ error: () => { /* expected */ } });

      const req = http.expectOne(`${API}/auth/refresh`);
      expect(req.request.body).toEqual({ refreshToken: '' });
      req.flush({}, { status: 401, statusText: 'Unauthorized' });
    });
  });
});
