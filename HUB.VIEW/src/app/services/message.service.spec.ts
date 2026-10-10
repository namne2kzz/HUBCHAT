import { fakeAsync, TestBed, tick } from '@angular/core/testing';
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

  describe('listAfter', () => {
    it('asks for messages after the anchor with the page size', () => {
      let received: unknown;
      service.listAfter('c1', 'm9', 100).subscribe(items => (received = items));

      // Reconnect catch-up: the anchor is a path segment, the page size a query param.
      const req = http.expectOne(r => r.url === `${MESSAGES}/after/m9`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('limit')).toBe('100');
      req.flush([{ id: 'm10' }]);

      expect(received).toEqual([{ id: 'm10' }]);
    });
  });

  describe('send', () => {
    it('posts the message to the channel', () => {
      const request = { body: 'hello', format: 1, parentId: null, mentionedUserIds: [] };
      service.send('c1', request as never).subscribe();

      const req = http.expectOne(MESSAGES);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(jasmine.objectContaining(request));
      req.flush({ id: 'm1' });
    });

    it('attaches a fresh client message id to every send', () => {
      service.send('c1', { body: 'a', format: 1 } as never).subscribe();
      service.send('c1', { body: 'a', format: 1 } as never).subscribe();

      // Two deliberate sends of the same text are two messages — they must not share a key.
      const [first, second] = http.match(MESSAGES);
      expect(first.request.body.clientMessageId).toMatch(/^[0-9a-f-]{36}$/);
      expect(second.request.body.clientMessageId).not.toBe(first.request.body.clientMessageId);
      first.flush({ id: 'm1' });
      second.flush({ id: 'm2' });
    });

    it('retries a lost response with the same key, so the server can return the original', fakeAsync(() => {
      let received: unknown;
      service.send('c1', { body: 'hi', format: 1 } as never).subscribe(m => (received = m));

      const attempt1 = http.expectOne(MESSAGES);
      const key = attempt1.request.body.clientMessageId;
      attempt1.error(new ProgressEvent('error'), { status: 0 }); // connection dropped — maybe committed

      tick(MessageService.retryBaseMs);
      const attempt2 = http.expectOne(MESSAGES);
      expect(attempt2.request.body.clientMessageId).toBe(key);
      attempt2.flush({ id: 'm1' });

      expect(received).toEqual({ id: 'm1' });
    }));

    it('does not retry a client error', fakeAsync(() => {
      let failed = false;
      service.send('c1', { body: 'hi', format: 1 } as never).subscribe({ error: () => (failed = true) });

      http.expectOne(MESSAGES).flush({ title: 'bad' }, { status: 400, statusText: 'Bad Request' });
      tick(10_000);

      // A 400 will fail identically every time; retrying only delays the error the user needs to see.
      http.expectNone(MESSAGES);
      expect(failed).toBeTrue();
    }));

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
