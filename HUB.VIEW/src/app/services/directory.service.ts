import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, shareReplay } from 'rxjs';
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
