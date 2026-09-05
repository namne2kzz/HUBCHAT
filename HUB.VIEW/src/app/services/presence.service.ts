import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, tap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ConfigService } from '../core/services/config.service';
import { RealtimeService } from './realtime.service';

export type PresenceStatus = 'Online' | 'Away' | 'DoNotDisturb' | 'Offline';

/**
 * Manages real-time user presence (online/away/offline).
 * Fetches bulk statuses from the REST endpoint and keeps them live via SignalR events.
 */
@Injectable({ providedIn: 'root' })
export class PresenceService {
  private readonly http    = inject(HttpClient);
  private readonly config  = inject(ConfigService);
  private readonly realtime = inject(RealtimeService);

  /** Map of userId → current presence status. Updated by REST + realtime events. */
  readonly presenceMap = signal<Record<string, PresenceStatus>>({});

  constructor() {
    // Keep map live whenever anyone's status changes via SignalR.
    this.realtime.presenceChanged$
      .pipe(takeUntilDestroyed())
      .subscribe(e => {
        this.presenceMap.update(m => ({ ...m, [e.userId]: e.status as PresenceStatus }));
      });
  }

  /**
   * Returns the current cached status for a user (defaults to Offline if not yet fetched).
   * @param userId User id.
   */
  getStatus(userId: string): PresenceStatus {
    return this.presenceMap()[userId] ?? 'Offline';
  }

  /**
   * Fetches and caches presence for the given user ids.
   * Safe to call with duplicates or empty arrays.
   * @param userIds List of user ids to fetch.
   * @returns Observable that completes after updating the map.
   */
  fetchStatuses(userIds: string[]): Observable<void> {
    const unique = [...new Set(userIds.filter(Boolean))];
    if (!unique.length) return new Observable(s => s.complete());

    const params = new HttpParams().set('userIds', unique.join(','));
    return this.http
      .get<Array<{ userId: string; status: string }>>(`${this.config.apiBaseUrl}/presence`, { params })
      .pipe(
        tap(rows => {
          const patch: Record<string, PresenceStatus> = {};
          rows.forEach(r => { patch[r.userId] = r.status as PresenceStatus; });
          this.presenceMap.update(m => ({ ...m, ...patch }));
        }),
        map(() => undefined),
      );
  }
}
