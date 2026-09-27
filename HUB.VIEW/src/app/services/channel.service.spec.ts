import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChannelService } from './channel.service';
import { ConfigService } from '../core/services/config.service';
import { ChannelDto } from '../models/channel.model';

/**
 * Covers ChannelService: the HTTP contract it speaks to the Chat API, and the local cache and
 * selection state the shell shares through it.
 *
 * The URL and verb assertions are not ceremony. Several of these endpoints are deliberately not the
 * obvious shape — adding another user is a PUT to /members/{userId} while joining yourself is a POST
 * to /members, because a single route could not tell the two apart. A "tidy-up" that unified them
 * would let any member add anybody to a channel.
 */
describe('ChannelService', () => {
  let service: ChannelService;
  let http: HttpTestingController;

  const API = 'http://localhost:5000/api/v1';
  const CHANNELS = `${API}/channels`;

  const channel = (over: Partial<ChannelDto> = {}): ChannelDto => ({
    id: 'c1', workspaceId: 'w1', name: 'General', slug: 'general', type: 0, topic: '',
    isPrivate: false, isArchived: false, memberCount: 1, createdAt: new Date().toISOString(),
    linkType: null, linkExternalKey: null, linkUrl: '', isMember: true,
    otherUserId: null, myRole: null, ...over,
  } as ChannelDto);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ConfigService, useValue: { apiBaseUrl: API, dashboardUrl: '', hubUrl: '' } },
      ],
    });

    localStorage.clear();
    service = TestBed.inject(ChannelService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  describe('reading', () => {
    it('lists channels for a workspace', () => {
      service.list('w1').subscribe();

      const req = http.expectOne(r => r.url === CHANNELS);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('workspaceId')).toBe('w1');
      req.flush([]);
    });

    it('gets one channel by id', () => {
      service.get('c1').subscribe();

      const req = http.expectOne(`${CHANNELS}/c1`);
      expect(req.request.method).toBe('GET');
      req.flush(channel());
    });

    it('lists a channel\'s members', () => {
      service.listMembers('c1').subscribe();

      const req = http.expectOne(`${CHANNELS}/c1/members`);
      expect(req.request.method).toBe('GET');
      req.flush([]);
    });
  });

  describe('writing', () => {
    it('creates a channel', () => {
      const request = { workspaceId: 'w1', name: 'New', type: 0, topic: '' };
      service.create(request as never).subscribe();

      const req = http.expectOne(CHANNELS);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(request);
      req.flush(channel());
    });

    it('updates name and topic with PATCH', () => {
      service.update('c1', { name: 'Renamed', topic: null } as never).subscribe();

      const req = http.expectOne(`${CHANNELS}/c1`);
      expect(req.request.method).toBe('PATCH');
      req.flush(channel());
    });

    it('changes visibility on its own sub-route', () => {
      service.changeVisibility('c1', true).subscribe();

      // Separate from the general update because the server requires Owner here and only
      // Admin-or-Owner for a rename.
      const req = http.expectOne(`${CHANNELS}/c1/visibility`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ isPrivate: true });
      req.flush(channel({ isPrivate: true }));
    });

    it('transfers ownership with PUT', () => {
      service.transferOwnership('c1', 'u2').subscribe();

      const req = http.expectOne(`${CHANNELS}/c1/owner`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ newOwnerUserId: 'u2' });
      req.flush(channel());
    });
  });

  describe('membership', () => {
    it('joins yourself with POST to /members', () => {
      service.join('c1').subscribe();

      const req = http.expectOne(`${CHANNELS}/c1/members`);
      expect(req.request.method).toBe('POST');
      req.flush(null);
    });

    it('leaves with DELETE to /members/me', () => {
      service.leave('c1').subscribe();

      // "me" rather than the caller's id: the server takes the user from the token, so a client
      // cannot remove somebody else through this route.
      const req = http.expectOne(`${CHANNELS}/c1/members/me`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });

    it('adds another user with PUT to /members/{userId}', () => {
      service.addMember('c1', { userId: 'u2' } as never).subscribe();

      // PUT to a different path than self-join, deliberately: one route could not distinguish
      // "let me in" from "let this person in", which need different permissions.
      const req = http.expectOne(`${CHANNELS}/c1/members/u2`);
      expect(req.request.method).toBe('PUT');
      req.flush({ userId: 'u2', role: 0, muted: false, joinedAt: '' });
    });

    it('removes another user with DELETE to /members/{userId}', () => {
      service.removeMember('c1', 'u2').subscribe();

      const req = http.expectOne(`${CHANNELS}/c1/members/u2`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });

  describe('linked channels and DMs', () => {
    it('opens a linked thread', () => {
      service.openLinkedThread({ workspaceId: 'w1', externalId: 'e1' } as never).subscribe();

      const req = http.expectOne(`${CHANNELS}/linked`);
      expect(req.request.method).toBe('POST');
      req.flush(channel());
    });

    it('opens a DM with the workspace as a query parameter', () => {
      service.openDm('w1', 'u2').subscribe();

      // The target user is in the path and the workspace in the query, because the workspace only
      // matters if the DM has to be created — an existing one is found regardless of it.
      const req = http.expectOne(r => r.url === `${CHANNELS}/dm/u2`);
      expect(req.request.method).toBe('POST');
      expect(req.request.params.get('workspaceId')).toBe('w1');
      req.flush(channel({ type: 2, otherUserId: 'u2' }));
    });
  });

  describe('local cache', () => {
    it('starts empty', () => {
      expect(service.channelsCache()).toBeNull();
    });

    it('finds a channel by slug after the cache is populated', () => {
      service.updateCache([channel({ id: 'c1', slug: 'general' }), channel({ id: 'c2', slug: 'random' })]);

      // The detail page resolves a slug from the URL this way instead of making another request.
      expect(service.findBySlug('random')?.id).toBe('c2');
    });

    it('finds a channel by id', () => {
      service.updateCache([channel({ id: 'c1' }), channel({ id: 'c2' })]);

      expect(service.findById('c2')?.id).toBe('c2');
    });

    it('returns undefined for a slug that is not cached', () => {
      service.updateCache([channel({ slug: 'general' })]);

      expect(service.findBySlug('missing')).toBeUndefined();
    });

    it('returns undefined before the cache is populated', () => {
      // Null cache and "not found" have to behave the same to callers, or the detail page throws on
      // a refresh that lands before the shell has loaded.
      expect(service.findBySlug('general')).toBeUndefined();
      expect(service.findById('c1')).toBeUndefined();
    });

    it('replaces the cache rather than appending to it', () => {
      service.updateCache([channel({ id: 'c1' })]);
      service.updateCache([channel({ id: 'c2' })]);

      expect(service.channelsCache()?.length).toBe(1);
      expect(service.findById('c1')).toBeUndefined();
    });
  });

  describe('selection', () => {
    it('records the selected channel', () => {
      service.selectChannel('c1');

      expect(service.selectedId()).toBe('c1');
    });

    it('persists the selection so a refresh reopens it', () => {
      service.selectChannel('c1');

      // The open channel is not in the URL, so without this a refresh drops the person back to no
      // channel selected.
      expect(localStorage.getItem('hub.selectedChannel')).toBe('c1');
    });

    it('restores the persisted selection on construction', () => {
      localStorage.setItem('hub.selectedChannel', 'c9');

      // A second injector, to construct the service afresh against the pre-seeded storage.
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
          { provide: ConfigService, useValue: { apiBaseUrl: API, dashboardUrl: '', hubUrl: '' } },
        ],
      });

      expect(TestBed.inject(ChannelService).selectedId()).toBe('c9');
      TestBed.inject(HttpTestingController).verify();
    });
  });
});
