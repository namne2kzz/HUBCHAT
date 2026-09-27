import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { StorageKeys } from '../core/constants/storage-keys.constant';

/**
 * Covers the interceptor that attaches the bearer token to outgoing requests.
 *
 * This runs on every single request, so the failure modes are broad: no header means the whole app
 * gets 401s, and a malformed one means the same while looking like a server problem.
 */
describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  const URL = '/api/v1/channels';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
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

  it('attaches the stored token as a bearer header', () => {
    localStorage.setItem(StorageKeys.accessToken, 'eyJ-token');

    http.get(URL).subscribe();

    const req = controller.expectOne(URL);
    expect(req.request.headers.get('Authorization')).toBe('Bearer eyJ-token');
    req.flush({});
  });

  it('sends no Authorization header when there is no token', () => {
    http.get(URL).subscribe();

    const req = controller.expectOne(URL);

    // An empty or literal "Bearer null" header would be worse than none: the server would try to
    // validate it and the failure would look like a bad token rather than an absent session.
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('does not wrap the token in quotes', () => {
    // Guards the same defect AuthService carries a comment about: a token stored with set() instead
    // of setString() arrives JSON-encoded and every request sends `Bearer "eyJ..."`.
    localStorage.setItem(StorageKeys.accessToken, 'eyJ-token');

    http.get(URL).subscribe();

    const req = controller.expectOne(URL);
    expect(req.request.headers.get('Authorization')).not.toContain('"');
    req.flush({});
  });

  it('leaves the rest of the request untouched', () => {
    localStorage.setItem(StorageKeys.accessToken, 'eyJ-token');

    http.post(URL, { name: 'General' }, { headers: { 'X-Custom': 'kept' } }).subscribe();

    const req = controller.expectOne(URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'General' });
    expect(req.request.headers.get('X-Custom')).toBe('kept');
    req.flush({});
  });

  it('attaches the token to every verb', () => {
    localStorage.setItem(StorageKeys.accessToken, 'eyJ-token');

    http.delete(URL).subscribe();
    http.patch(URL, {}).subscribe();

    const requests = controller.match(URL);
    expect(requests.length).toBe(2);

    for (const req of requests) {
      expect(req.request.headers.get('Authorization')).toBe('Bearer eyJ-token');
      req.flush({});
    }
  });

  it('reads the token fresh on each request', () => {
    // The token rotates on refresh, so caching it at interceptor construction would keep sending the
    // old one until a reload.
    localStorage.setItem(StorageKeys.accessToken, 'first');
    http.get(URL).subscribe();
    const first = controller.expectOne(URL);
    expect(first.request.headers.get('Authorization')).toBe('Bearer first');
    first.flush({});

    localStorage.setItem(StorageKeys.accessToken, 'second');
    http.get(URL).subscribe();
    const second = controller.expectOne(URL);
    expect(second.request.headers.get('Authorization')).toBe('Bearer second');
    second.flush({});
  });
});
