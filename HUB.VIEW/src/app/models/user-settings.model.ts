/** User-chosen presence status shown to others. Maps to the server PresenceStatus enum. */
export type UserStatus = 'Active' | 'Away' | 'DoNotDisturb';

/** Numeric PresenceStatus values expected by the realtime hub's SetStatus method. */
export const STATUS_CODE: Record<UserStatus, number> = {
  Active: 1,       // Online / auto (clears any override)
  Away: 2,
  DoNotDisturb: 3,
};

/**
 * "Quiet hours" schedule: when enabled, notification sound + browser popups are suppressed
 * during the [start, end) window (wraps past midnight, e.g. 22:00 → 08:00). Client-side only.
 */
export interface QuietHours {
  enabled: boolean;
  /** Inclusive start time, "HH:mm" (24h). */
  start: string;
  /** Exclusive end time, "HH:mm" (24h). */
  end: string;
}

/**
 * Notification preferences beyond presence. Client-side only (localStorage), like QuietHours.
 * - `mutedUntilMs`: epoch ms; while now < this, notifications are off ("turn off until…"). 0 = not set.
 * - `onlyWhenMentioned`: when notifications are otherwise muted, still notify for messages that @mention
 *   the current user. Turning off the mute entirely silences everything; this is the one exception.
 */
export interface NotificationPrefs {
  mutedUntilMs: number;
  onlyWhenMentioned: boolean;
}

/** Preset "turn off until" durations offered in settings. */
export type MuteDuration = '30m' | '1h' | '4h' | 'tomorrow' | 'forever';
