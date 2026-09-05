import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { RealtimeService } from './realtime.service';
import { QuietHours, STATUS_CODE, UserStatus } from '../models/user-settings.model';

const STATUS_KEY = 'hub.userStatus';
const QUIET_KEY  = 'hub.quietHours';

/**
 * Holds per-user chat settings: manual presence status (Active/Away/Do Not Disturb) and a
 * client-side "quiet hours" notification schedule. Status is synced to the realtime hub and
 * re-applied on every (re)connect; both values persist in localStorage (per device).
 */
@Injectable({ providedIn: 'root' })
export class UserSettingsService {
  private readonly realtime = inject(RealtimeService);

  /** The user's chosen presence status. */
  readonly status = signal<UserStatus>(this.loadStatus());

  /** Quiet-hours schedule (client-side notification muting). */
  readonly quietHours = signal<QuietHours>(this.loadQuiet());

  /** Ticks every minute so {@link muted} re-evaluates the quiet-hours window over time. */
  private readonly minuteTick = signal(0);

  /**
   * True when notifications should be silenced: either the user is in Do Not Disturb,
   * or quiet hours are enabled and the current time falls inside the window.
   */
  readonly muted = computed(() => {
    this.minuteTick(); // re-evaluate as time passes
    if (this.status() === 'DoNotDisturb') return true;
    const q = this.quietHours();
    return q.enabled && this.inWindow(q.start, q.end);
  });

  constructor() {
    // Re-apply the chosen status to the server whenever the connection (re)establishes.
    effect(() => {
      if (this.realtime.connectionState() === 'connected') {
        void this.realtime.setStatus(STATUS_CODE[this.status()]);
      }
    });

    setInterval(() => this.minuteTick.update(n => n + 1), 60_000);
  }

  /** Sets the user's presence status, persists it, and syncs to the server. @param status New status. */
  setStatus(status: UserStatus): void {
    this.status.set(status);
    try { localStorage.setItem(STATUS_KEY, status); } catch { /* storage unavailable */ }
    void this.realtime.setStatus(STATUS_CODE[status]);
  }

  /** Updates the quiet-hours schedule and persists it. @param patch Partial quiet-hours settings. */
  setQuietHours(patch: Partial<QuietHours>): void {
    const next = { ...this.quietHours(), ...patch };
    this.quietHours.set(next);
    try { localStorage.setItem(QUIET_KEY, JSON.stringify(next)); } catch { /* storage unavailable */ }
  }

  /** True when "now" (local time) falls within [start, end), handling windows that wrap past midnight. */
  private inWindow(start: string, end: string): boolean {
    const now = new Date();
    const cur = now.getHours() * 60 + now.getMinutes();
    const s = this.toMinutes(start), e = this.toMinutes(end);
    if (s === e) return false;
    return s < e ? cur >= s && cur < e : cur >= s || cur < e; // wraps midnight when s > e
  }

  /** Parses "HH:mm" into minutes-since-midnight (0 on malformed input). */
  private toMinutes(hhmm: string): number {
    const [h, m] = hhmm.split(':').map(Number);
    return (Number.isFinite(h) ? h : 0) * 60 + (Number.isFinite(m) ? m : 0);
  }

  private loadStatus(): UserStatus {
    try {
      const v = localStorage.getItem(STATUS_KEY);
      if (v === 'Active' || v === 'Away' || v === 'DoNotDisturb') return v;
    } catch { /* storage unavailable */ }
    return 'Active';
  }

  private loadQuiet(): QuietHours {
    try {
      const raw = localStorage.getItem(QUIET_KEY);
      if (raw) {
        const p = JSON.parse(raw) as Partial<QuietHours>;
        return {
          enabled: !!p.enabled,
          start: typeof p.start === 'string' ? p.start : '22:00',
          end:   typeof p.end   === 'string' ? p.end   : '08:00',
        };
      }
    } catch { /* malformed or unavailable */ }
    return { enabled: false, start: '22:00', end: '08:00' };
  }
}
