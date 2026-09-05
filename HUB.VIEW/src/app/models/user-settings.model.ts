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
