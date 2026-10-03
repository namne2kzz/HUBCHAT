import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { forkJoin, map, Observable, of, shareReplay, tap } from 'rxjs';
import { ConfigService } from '../core/services/config.service';
import { DirectoryUser, UserMemberships, WorkItemContext } from '../models/directory.model';

/**
 * Resolves DASHBOARD directory data (users, memberships, work items) via the HUB dashboard-gateway.
 * User lookups are memoised for the session — author names don't change mid-session.
 */
@Injectable({ providedIn: 'root' })
export class DirectoryService {
  private readonly http   = inject(HttpClient);
  private readonly config = inject(ConfigService);
  private get apiUrl() { return `${this.config.apiBaseUrl}/directory`; }

  private readonly userCache = new Map<string, Observable<DirectoryUser>>();

  /**
   * Gets a user's profile (name + avatar class). Memoised per id.
   * @param id User id.
   * @returns Observable of the profile.
   */
  getUser(id: string): Observable<DirectoryUser> {
    let cached = this.userCache.get(id);
    if (!cached) {
      cached = this.http.get<DirectoryUser>(`${this.apiUrl}/users/${id}`).pipe(shareReplay(1));
      this.userCache.set(id, cached);
    }
    return cached;
  }

  /**
   * Gets several user profiles in one request, reusing whatever {@link getUser} already memoised.
   * @param ids User ids; duplicates are ignored.
   * @returns Observable of the profiles that exist, in no guaranteed order.
   */
  getUsers(ids: readonly string[]): Observable<DirectoryUser[]> {
    const wanted = [...new Set(ids)];
    if (!wanted.length) return of([]);

    const cached  = wanted.filter(id => this.userCache.has(id));
    const missing = wanted.filter(id => !this.userCache.has(id));

    // Every id already memoised — no request at all.
    if (!missing.length) return forkJoin(cached.map(id => this.getUser(id)));

    // One request for the whole missing set, then seeded into the per-id cache so a later getUser()
    // for any of them is served without another round-trip.
    const fetched$ = this.http
      .get<DirectoryUser[]>(`${this.apiUrl}/users`, { params: { ids: missing.join(',') } })
      .pipe(
        tap(profiles => profiles.forEach(p => this.userCache.set(p.id, of(p).pipe(shareReplay(1))))),
        shareReplay(1),
      );

    return cached.length
      ? forkJoin([fetched$, ...cached.map(id => this.getUser(id))]).pipe(
          map(([fresh, ...rest]) => [...fresh, ...rest]))
      : fetched$;
  }

  /** Gets the caller's repository/workspace memberships. @returns Observable of memberships. */
  getMyMemberships(): Observable<UserMemberships> {
    return this.http.get<UserMemberships>(`${this.apiUrl}/me/memberships`);
  }

  /**
   * Lists all member profiles for a workspace (repository).
   * Uses the HUB dashboard-gateway which caches the result for 5 minutes.
   * @param workspaceId The workspace (repository) id.
   * @returns Observable of member profiles.
   */
  listWorkspaceMembers(workspaceId: string): Observable<DirectoryUser[]> {
    return this.http.get<DirectoryUser[]>(`${this.apiUrl}/workspaces/${workspaceId}/members`);
  }

  /** Gets a work item's context for linking a discussion thread. @param id Work item id. */
  getWorkItem(id: string): Observable<WorkItemContext | null> {
    return this.http.get<WorkItemContext | null>(`${this.apiUrl}/work-items/${id}`);
  }

  /** Empty-safe helper returning of(null) for missing ids. @param id Optional id. */
  getUserOrNull(id: string | null): Observable<DirectoryUser | null> {
    return id ? this.getUser(id) : of(null);
  }
}
