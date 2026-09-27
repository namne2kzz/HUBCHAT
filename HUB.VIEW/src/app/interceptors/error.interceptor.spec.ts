import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ToastrService } from 'ngx-toastr';
import { errorInterceptor } from './error.interceptor';
import { ConfigService } from '../core/services/config.service';
import { NavigationService } from '../core/services/navigation.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';

/**
 * Covers the global HTTP error handling: which failures reach the person as a toast, and what a 401
 * does to the stored session.
 *
 * The 401 branch navigates to DASHBOARD, which is a real page load rather than a router hop. It goes
 * through NavigationService so it can be substituted here: assigning window.location.href directly
 * unloads the Karma page and the run dies with "Disconnected, because no message in 30000 ms" — which
 * is what happened before that seam existed, and `window.location` turned out to be non-configurable
 * in this browser, so it could not simply be stubbed.
 */
describe('errorInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let toastr: jasmine.SpyObj<ToastrService>;

  let navigation: jasmine.SpyObj<NavigationService>;

  const URL = '/api/v1/channels';
  const DASHBOARD = 'http://dashboard.test';
  const HERE = 'http://hub.test/channels';

  beforeEach(() => {
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', ['error']);
    navigation = jasmine.createSpyObj<NavigationService>('NavigationService', ['goTo', 'currentUrl', 'origin']);
    navigation.currentUrl.and.returnValue(HERE);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastrService, useValue: toastr },
        { provide: NavigationService, useValue: navigation },
        { provide: ConfigService, useValue: { apiBaseUrl: '/api/v1', dashboardUrl: DASHBOARD, hubUrl: '' } },
      ],
    });

    localStorage.clear();
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  /** Fires a request and captures the error the caller receives. */
  const failWith = (status: number, body: Record<string, unknown> | null = {}): Promise<HttpErrorResponse> =>
    new Promise(resolve => {
      http.get(URL).subscribe({ error: (e: HttpErrorResponse) => resolve(e) });
      controller.expectOne(URL).flush(body, { status, statusText: 'Error' });
    });

  describe('surfacing errors', () => {
    it('toasts a 500 with its status in the title', async () => {
      await failWith(500, { detail: 'Something broke' });

      expect(toastr.error).toHaveBeenCalledWith('Something broke', 'Error 500');
    });

    it('prefers the problem-details detail field', async () => {
      // ASP.NET problem details put the useful sentence in `detail`; `title` is the generic category.
      await failWith(400, { detail: 'Body is required', title: 'Bad Request' });

      expect(toastr.error).toHaveBeenCalledWith('Body is required', 'Error 400');
    });

    it('falls back to title when there is no detail', async () => {
      await failWith(400, { title: 'Bad Request' });

      expect(toastr.error).toHaveBeenCalledWith('Bad Request', 'Error 400');
    });

    it('falls back to a generic message when the body says nothing useful', async () => {
      await failWith(503, null);

      // Never an empty toast: a message the person cannot read is worse than a generic one.
      const [message] = toastr.error.calls.mostRecent().args;
      expect(message).toBeTruthy();
    });

    it('stays silent on a status of 0', async () => {
      // Status 0 means the request never completed — a dropped connection, a navigation away, or a
      // cancelled request. Toasting those would fire an error every time somebody closes the tab.
      await failWith(0);

      expect(toastr.error).not.toHaveBeenCalled();
    });

    it('re-throws so the caller can still handle the failure', async () => {
      const error = await failWith(404, { detail: 'Not found' });

      // The interceptor reports, it does not swallow: a component that wants to show inline state
      // still gets its error callback.
      expect(error.status).toBe(404);
    });
  });

  describe('on 401', () => {
    it('clears the stored session', async () => {
      localStorage.setItem(StorageKeys.accessToken, 'stale');
      localStorage.setItem(StorageKeys.refreshToken, 'stale-refresh');
      localStorage.setItem(StorageKeys.userProfile, '{}');

      await failWith(401);

      // Leaving a rejected token behind would make the app retry with it on the next page load and
      // loop through the same failure.
      expect(localStorage.getItem(StorageKeys.accessToken)).toBeNull();
      expect(localStorage.getItem(StorageKeys.refreshToken)).toBeNull();
      expect(localStorage.getItem(StorageKeys.userProfile)).toBeNull();
    });

    it('does not toast', async () => {
      // A 401 sends the person to DASHBOARD to sign in again, so an error toast would flash and then
      // be thrown away by the navigation.
      await failWith(401);

      expect(toastr.error).not.toHaveBeenCalled();
    });

    it('re-throws the 401', async () => {
      const error = await failWith(401);

      expect(error.status).toBe(401);
    });

    it('redirects to the DASHBOARD login', async () => {
      // HUB has no login of its own, so a rejected token means handing the person back to DASHBOARD.
      await failWith(401);

      expect(navigation.goTo).toHaveBeenCalledWith(
        `${DASHBOARD}/login?returnUrl=${encodeURIComponent(HERE)}`);
    });

    it('does not redirect on other statuses', async () => {
      // A 500 or a 404 is a problem with one request, not with the session — bouncing the person to
      // a login screen would lose whatever they were doing.
      await failWith(500);

      expect(navigation.goTo).not.toHaveBeenCalled();
    });
  });

  describe('on success', () => {
    it('passes a successful response straight through', () => {
      let body: unknown;
      http.get(URL).subscribe(r => (body = r));

      controller.expectOne(URL).flush({ ok: true });

      expect(body).toEqual({ ok: true });
      expect(toastr.error).not.toHaveBeenCalled();
    });
  });
});
