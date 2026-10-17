import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Subject } from 'rxjs';
import { PresenceService } from './presence.service';
import { RealtimeService } from './realtime.service';
import { ConfigService } from '../core/services/config.service';
import { CustomStatusChangedEvent, PresenceChangedEvent } from '../models/presence.model';
import { SKIP_ERROR_TOAST } from '../core/http/http-context';

/**
 * Covers PresenceService custom-status handling (CF-19): fetch, realtime set/clear, and the on-read
 * expiry check that protects against a status that lapsed between fetches.
 */
describe('PresenceService — custom status', () => {
  let service: PresenceService;
  let http: HttpTestingController;
  let presenceChanged$: Subject<PresenceChangedEvent>;
  let customStatusChanged$: Subject<CustomStatusChangedEvent>;
  let setCustomStatus: jasmine.Spy;

  const API = 'http://localhost:5000/api/v1';

  beforeEach(() => {
    presenceChanged$ = new Subject();
    customStatusChanged$ = new Subject();
    setCustomStatus = jasmine.createSpy('setCustomStatus').and.resolveTo(undefined);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ConfigService, useValue: { apiBaseUrl: API, dashboardUrl: '', hubUrl: '' } },
        { provide: RealtimeService, useValue: { presenceChanged$, customStatusChanged$, setCustomStatus } },
      ],
    });

    service = TestBed.inject(PresenceService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('fetches custom statuses and exposes them by user id', () => {
    service.fetchCustomStatuses(['u1', 'u2']).subscribe();

    const req = http.expectOne(r => r.url === `${API}/presence/custom`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('userIds')).toBe('u1,u2');
    req.flush([{ userId: 'u1', emoji: '📅', text: 'In a meeting', expiresAt: null }]);

    expect(service.getCustomStatus('u1')).toEqual({ emoji: '📅', text: 'In a meeting', expiresAt: null });
    expect(service.getCustomStatus('u2')).toBeNull();
  });

  it('does not call the API for an empty id list', () => {
    service.fetchCustomStatuses([]).subscribe();
    http.expectNone(`${API}/presence/custom`);
  });

  it('degrades silently on a 404 (old/absent realtime service) without erroring the subscriber', () => {
    let errored = false;
    let completed = false;
    service.fetchCustomStatuses(['u1']).subscribe({ error: () => errored = true, complete: () => completed = true });

    const req = http.expectOne(r => r.url === `${API}/presence/custom`);
    // The request opts out of the global error toast; the stream must still complete cleanly.
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush('nope', { status: 404, statusText: 'Not Found' });

    expect(errored).toBeFalse();
    expect(completed).toBeTrue();
    expect(service.getCustomStatus('u1')).toBeNull();
  });

  it('applies a realtime set and removes on an empty-text clear', () => {
    customStatusChanged$.next({ userId: 'u1', emoji: '🍔', text: 'Lunch', expiresAt: null });
    expect(service.getCustomStatus('u1')?.text).toBe('Lunch');

    customStatusChanged$.next({ userId: 'u1', emoji: '', text: '', expiresAt: null });
    expect(service.getCustomStatus('u1')).toBeNull();
  });

  it('treats an expired status as absent on read', () => {
    const past = new Date(Date.now() - 1000).toISOString();
    customStatusChanged$.next({ userId: 'u1', emoji: '🍔', text: 'Lunch', expiresAt: past });

    // The map still holds it, but getCustomStatus hides it because its clock passed.
    expect(service.getCustomStatus('u1')).toBeNull();
  });

  it('keeps a not-yet-expired status visible', () => {
    const future = new Date(Date.now() + 60_000).toISOString();
    customStatusChanged$.next({ userId: 'u1', emoji: '🍔', text: 'Lunch', expiresAt: future });

    expect(service.getCustomStatus('u1')?.text).toBe('Lunch');
  });

  it('forwards setCustomStatus to realtime with a Unix-ms expiry', async () => {
    const when = new Date(Date.now() + 30 * 60_000);
    await service.setCustomStatus('📅', 'Heads down', when);

    expect(setCustomStatus).toHaveBeenCalledWith('📅', 'Heads down', when.getTime());
  });

  it('forwards a null expiry as null', async () => {
    await service.setCustomStatus('📅', 'Heads down', null);
    expect(setCustomStatus).toHaveBeenCalledWith('📅', 'Heads down', null);
  });
});
