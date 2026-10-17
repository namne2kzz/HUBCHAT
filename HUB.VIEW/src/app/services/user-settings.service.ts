import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { RealtimeService } from './realtime.service';
import { MuteDuration, NotificationPrefs, QuietHours, STATUS_CODE, UserStatus } from '../models/user-settings.model';

const STATUS_KEY = 'hub.userStatus';
const QUIET_KEY  = 'hub.quietHours';
const PREFS_KEY  = 'hub.notifPrefs';

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
  readonly prefs = signal<NotificationPrefs>(this.loadPrefs());

  /** True while a "turn off until…" mute is in effect. */
  readonly mutedUntilActive = computed(() => {
    this.minuteTick();
    const until = this.prefs().mutedUntilMs;
    return until > 0 && Date.now() < until;
  });

  /** Whether notifications are suppressed right now (DnD, quiet hours, or a "turn off until" mute). */
  readonly muted = computed(() => {
    this.minuteTick(); // re-evaluate as time passes
    if (this.status() === 'DoNotDisturb') return true;
    if (this.mutedUntilActive()) return true;
    const q = this.quietHours();
    return q.enabled && this.inWindow(q.start, q.end);
  });

  /**
   * Whether a just-arrived message should notify (sound + unread bump).
   * Muted silences everything, EXCEPT: with "only when mentioned" on, a message that @mentions you still
   * notifies. Not muted → always notify.
   * @param mentionsMe True when the message @mentions the current user.
   * @returns Whether to notify.
   */
  shouldNotify(mentionsMe: boolean): boolean {
    if (!this.muted()) return true;
    return this.prefs().onlyWhenMentioned && mentionsMe;
  }

  /**
   * Whether a just-arrived notification should surface live (slide into the panel, bump the badge, beep).
   * Same rule as {@link shouldNotify}: muted silences everything unless "only when mentioned" is on and
   * this is a mention/DM. A muted-and-not-mention notification is dropped from the live surface entirely —
   * so turning the toggle off truly silences, not "still shows in the list".
   * @param isMentionLike True for Mention / DirectMessage notifications (the kinds a mention toggle keeps).
   * @returns Whether to surface it.
   */
  shouldSurfaceNotification(isMentionLike: boolean): boolean {
    return this.shouldNotify(isMentionLike);
  }

  /** Turns notifications off for a preset duration. @param d Duration preset. */
  muteFor(d: MuteDuration): void {
    const now = new Date();
    let until: number;
    switch (d) {
      case '30m': until = now.getTime() + 30 * 60_000; break;
      case '1h':  until = now.getTime() + 60 * 60_000; break;
      case '4h':  until = now.getTime() + 4 * 60 * 60_000; break;
      case 'tomorrow': {
        const t = new Date(now); t.setDate(t.getDate() + 1); t.setHours(8, 0, 0, 0); until = t.getTime(); break;
      }
      case 'forever': until = Number.MAX_SAFE_INTEGER; break;
    }
    this.patchPrefs({ mutedUntilMs: until });
  }

  /** Clears any "turn off until" mute. */
  unmute(): void { this.patchPrefs({ mutedUntilMs: 0 }); }

  /** Sets whether to still notify for @mentions while muted. @param on Enabled. */
  setOnlyWhenMentioned(on: boolean): void { this.patchPrefs({ onlyWhenMentioned: on }); }

  private patchPrefs(patch: Partial<NotificationPrefs>): void {
    const next = { ...this.prefs(), ...patch };
    this.prefs.set(next);
    try { localStorage.setItem(PREFS_KEY, JSON.stringify(next)); } catch { /* storage unavailable */ }
  }

  private loadPrefs(): NotificationPrefs {
    try {
      const raw = localStorage.getItem(PREFS_KEY);
      if (raw) {
        const p = JSON.parse(raw) as Partial<NotificationPrefs>;
        return {
          mutedUntilMs: typeof p.mutedUntilMs === 'number' ? p.mutedUntilMs : 0,
          onlyWhenMentioned: !!p.onlyWhenMentioned,
        };
      }
    } catch { /* malformed or unavailable */ }
    return { mutedUntilMs: 0, onlyWhenMentioned: false };
  }

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
