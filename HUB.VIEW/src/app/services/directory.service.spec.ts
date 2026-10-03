import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DirectoryService } from './directory.service';
import { ConfigService } from '../core/services/config.service';
import { DirectoryUser } from '../models/directory.model';

/**
 * Covers DirectoryService — the per-session memoisation of DASHBOARD profiles, and the batch lookup
 * that replaced a request-per-member.
 *
 * The batch path is where the risk sits. A channel member list or a page of messages resolves many
 * authors at once, and the old code issued one request per id: a 30-member channel meant 30 requests
 * for data that rarely changes. What these tests pin is that the batch and single paths share one
 * cache — if they did not, the batch would warm entries nobody reads and `getUser` would refetch every
 * profile the batch had just loaded, leaving the N+1 in place behind a batch-shaped API.
 */
describe('DirectoryService', () => {
  let service: DirectoryService;
  let http: HttpTestingController;

  const API = 'http://localhost:5000/api/v1';
  const DIR = `${API}/directory`;

  const user = (id: string, name = `User ${id}`): DirectoryUser => ({
    id, name, email: `${id}@x.com`, avatarClass: 'bg-sky-600', isGlobalAdmin: false, isDeleted: false,
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ConfigService, useValue: { apiBaseUrl: API, dashboardUrl: '', hubUrl: '' } },
      ],
    });

    service = TestBed.inject(DirectoryService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('getUser', () => {
    it('memoises a profile so a second lookup issues no request', () => {
      service.getUser('u1').subscribe();
      http.expectOne(`${DIR}/users/u1`).flush(user('u1'));

      let name: string | undefined;
      service.getUser('u1').subscribe(u => (name = u.name));

      expect(name).toBe('User u1');
      http.expectNone(`${DIR}/users/u1`);
    });
  });

  describe('getUsers', () => {
    it('fetches every id in one request', () => {
      let result: DirectoryUser[] = [];
      service.getUsers(['u1', 'u2', 'u3']).subscribe(r => (result = r));

      const req = http.expectOne(r => r.url === `${DIR}/users`);
      expect(req.request.params.get('ids')).toBe('u1,u2,u3');
      req.flush([user('u1'), user('u2'), user('u3')]);

      expect(result.map(u => u.id)).toEqual(['u1', 'u2', 'u3']);
    });

    it('deduplicates ids before asking', () => {
      service.getUsers(['u1', 'u2', 'u1']).subscribe();

      const req = http.expectOne(r => r.url === `${DIR}/users`);
      expect(req.request.params.get('ids')).toBe('u1,u2');
      req.flush([user('u1'), user('u2')]);
    });

    it('asks only for the ids not already memoised by getUser', () => {
      service.getUser('u1').subscribe();
      http.expectOne(`${DIR}/users/u1`).flush(user('u1'));

      let result: DirectoryUser[] = [];
      service.getUsers(['u1', 'u2']).subscribe(r => (result = r));

      const req = http.expectOne(r => r.url === `${DIR}/users`);
      expect(req.request.params.get('ids')).toBe('u2');
      req.flush([user('u2')]);

      // Both profiles come back even though only one was fetched here.
      expect(result.map(u => u.id).sort()).toEqual(['u1', 'u2']);
    });

    it('issues no request at all when every id is already cached', () => {
      service.getUser('u1').subscribe();
      http.expectOne(`${DIR}/users/u1`).flush(user('u1'));

      let result: DirectoryUser[] = [];
      service.getUsers(['u1']).subscribe(r => (result = r));

      expect(result.map(u => u.id)).toEqual(['u1']);
      http.expectNone(r => r.url === `${DIR}/users`);
    });

    it('seeds the cache so a later getUser needs no request', () => {
      service.getUsers(['u1', 'u2']).subscribe();
      http.expectOne(r => r.url === `${DIR}/users`).flush([user('u1'), user('u2')]);

      let name: string | undefined;
      service.getUser('u1').subscribe(u => (name = u.name));

      // The point of sharing one cache: without this the batch would leave the N+1 in place.
      expect(name).toBe('User u1');
      http.expectNone(`${DIR}/users/u1`);
    });

    it('returns an empty list without a request when given no ids', () => {
      let result: DirectoryUser[] | undefined;
      service.getUsers([]).subscribe(r => (result = r));

      expect(result).toEqual([]);
      http.expectNone(r => r.url === `${DIR}/users`);
    });

    it('omits ids the server did not return, rather than inventing profiles', () => {
      let result: DirectoryUser[] = [];
      service.getUsers(['u1', 'deleted']).subscribe(r => (result = r));

      http.expectOne(r => r.url === `${DIR}/users`).flush([user('u1')]);

      expect(result.map(u => u.id)).toEqual(['u1']);
    });
  });
});
