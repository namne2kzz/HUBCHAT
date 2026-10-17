import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ConfigService } from '../core/services/config.service';
import { RealtimeService } from './realtime.service';
import { CustomStatus } from '../models/presence.model';
import { skipErrorToast } from '../core/http/http-context';

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

  /** Map of userId → custom status (CF-19). Only users with a live status appear. */
  readonly customStatusMap = signal<Record<string, CustomStatus>>({});

  constructor() {
    // Keep map live whenever anyone's status changes via SignalR.
    this.realtime.presenceChanged$
      .pipe(takeUntilDestroyed())
      .subscribe(e => {
        this.presenceMap.update(m => ({ ...m, [e.userId]: e.status as PresenceStatus }));
      });

    // Custom status: an empty note means the user cleared it, so drop the entry rather than show a blank.
    this.realtime.customStatusChanged$
      .pipe(takeUntilDestroyed())
      .subscribe(e => {
        if (!e.text && !e.emoji) {
          this.customStatusMap.update(m => { const { [e.userId]: _, ...rest } = m; return rest; });
        } else {
          this.customStatusMap.update(m => ({
            ...m,
            [e.userId]: { emoji: e.emoji, text: e.text, expiresAt: e.expiresAt },
          }));
        }
      });
  }

  /**
   * Returns a user's custom status if set and not expired; null otherwise (CF-19).
   * The expiry is re-checked on read because the server's Redis TTL and the client clock can drift,
   * and a status can lapse between fetches without any event arriving.
   * @param userId User id.
   */
  getCustomStatus(userId: string): CustomStatus | null {
    const cs = this.customStatusMap()[userId];
    if (!cs) return null;
    if (cs.expiresAt && Date.parse(cs.expiresAt) <= Date.now()) return null;
    return cs;
  }

  /**
   * Fetches and caches custom statuses for the given user ids (CF-19).
   * @param userIds List of user ids.
   * @returns Observable that completes after updating the map.
   */
  fetchCustomStatuses(userIds: string[]): Observable<void> {
    const unique = [...new Set(userIds.filter(Boolean))];
    if (!unique.length) return new Observable(s => s.complete());

    const params = new HttpParams().set('userIds', unique.join(','));
    return this.http
      .get<Array<CustomStatus & { userId: string }>>(`${this.config.apiBaseUrl}/presence/custom`,
        { params, context: skipErrorToast() })
      .pipe(
        tap(rows => {
          const patch: Record<string, CustomStatus> = {};
          rows.forEach(r => { patch[r.userId] = { emoji: r.emoji, text: r.text, expiresAt: r.expiresAt }; });
          this.customStatusMap.update(m => ({ ...m, ...patch }));
        }),
        map(() => undefined),
        // Custom status is a nice-to-have; a missing/old realtime service (404) or outage must degrade
        // silently, not fire a red error toast through the global interceptor.
        catchError(() => of(undefined)),
      );
  }

  /**
   * Sets (or clears) the current user's custom status via realtime (CF-19).
   * @param emoji A single emoji, or ''.
   * @param text The note, or '' to clear.
   * @param expiresAt Auto-clear time, or null for "don't clear".
   */
  async setCustomStatus(emoji: string, text: string, expiresAt: Date | null): Promise<void> {
    await this.realtime.setCustomStatus(emoji, text, expiresAt ? expiresAt.getTime() : null);
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
