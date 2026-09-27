import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MessageService } from './message.service';
import { ConfigService } from '../core/services/config.service';

/**
 * Covers MessageService: the paging contract and the two write endpoints.
 *
 * The cursor handling is the part worth pinning. The server pages backwards through history with an
 * opaque keyset cursor, and sending an empty one is not the same as sending none — the first page is
 * requested by omitting the parameter entirely.
 */
describe('MessageService', () => {
  let service: MessageService;
  let http: HttpTestingController;

  const API = 'http://localhost:5000/api/v1';
  const MESSAGES = `${API}/channels/c1/messages`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ConfigService, useValue: { apiBaseUrl: API, dashboardUrl: '', hubUrl: '' } },
      ],
    });

    service = TestBed.inject(MessageService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('list', () => {
    it('requests the first page without a cursor parameter', () => {
      service.list('c1').subscribe();

      const req = http.expectOne(r => r.url === MESSAGES);
      expect(req.request.method).toBe('GET');

      // Absent, not empty: an empty cursor would be decoded server-side and, before BUG-001 was
      // fixed, a malformed one could reach the catch-all.
      expect(req.request.params.has('cursor')).toBeFalse();
      req.flush({ items: [], nextCursor: null });
    });

    it('defaults to a page size of 50', () => {
      service.list('c1').subscribe();

      const req = http.expectOne(r => r.url === MESSAGES);
      expect(req.request.params.get('limit')).toBe('50');
      req.flush({ items: [], nextCursor: null });
    });

    it('passes an explicit page size', () => {
      service.list('c1', undefined, 20).subscribe();

      const req = http.expectOne(r => r.url === MESSAGES);
      expect(req.request.params.get('limit')).toBe('20');
      req.flush({ items: [], nextCursor: null });
    });

    it('sends the cursor when paging further back', () => {
      service.list('c1', 'opaque-cursor').subscribe();

      const req = http.expectOne(r => r.url === MESSAGES);
      expect(req.request.params.get('cursor')).toBe('opaque-cursor');
      req.flush({ items: [], nextCursor: null });
    });

    it('returns the page as the server sent it', () => {
      let received: unknown;
      service.list('c1').subscribe(page => (received = page));

      const page = { items: [{ id: 'm1', body: 'hello' }], nextCursor: 'next' };
      http.expectOne(r => r.url === MESSAGES).flush(page);

      // No client-side reshaping, so the cursor survives to be handed straight back on the next call.
      expect(received).toEqual(page);
    });

    it('targets the channel given to it', () => {
      service.list('other-channel').subscribe();

      const req = http.expectOne(r => r.url === `${API}/channels/other-channel/messages`);
      expect(req.request.url).toBe(`${API}/channels/other-channel/messages`);
      req.flush({ items: [], nextCursor: null });
    });
  });

  describe('send', () => {
    it('posts the message to the channel', () => {
      const request = { body: 'hello', format: 1, parentId: null, mentionedUserIds: [] };
      service.send('c1', request as never).subscribe();

      const req = http.expectOne(MESSAGES);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(request);
      req.flush({ id: 'm1' });
    });

    it('sends the format as a number', () => {
      // The API registers no JsonStringEnumConverter, so an enum name would be rejected at model
      // binding with a 400 that never reaches the handler.
      service.send('c1', { body: 'x', format: 1, parentId: null, mentionedUserIds: [] } as never).subscribe();

      const req = http.expectOne(MESSAGES);
      expect(typeof req.request.body.format).toBe('number');
      req.flush({ id: 'm1' });
    });

    it('carries a parent id for a thread reply', () => {
      service.send('c1', { body: 'reply', format: 1, parentId: 'm0', mentionedUserIds: [] } as never).subscribe();

      const req = http.expectOne(MESSAGES);
      expect(req.request.body.parentId).toBe('m0');
      req.flush({ id: 'm1' });
    });
  });

  describe('react', () => {
    it('posts the emoji to the message, not the channel', () => {
      service.react('m1', ':+1:').subscribe();

      // Reactions hang off the message rather than the channel, so the route has no channel segment.
      const req = http.expectOne(`${API}/messages/m1/reactions`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ emoji: ':+1:' });
      req.flush(null);
    });
  });
});
