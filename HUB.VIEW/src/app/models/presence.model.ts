export interface PresenceDto {
  userId: string;
  status: 'Online' | 'Away' | 'Offline';
  lastSeenAt: string | null;
}

/** Realtime event pushed via SignalR. */
export interface PresenceChangedEvent {
  userId: string;
  status: 'Online' | 'Away' | 'Offline';
}

/** A user's custom status (CF-19) — emoji + note + optional auto-clear time. Empty text means "none". */
export interface CustomStatus {
  emoji: string;
  text: string;
  /** ISO timestamp when it auto-clears, or null for "don't clear". */
  expiresAt: string | null;
}

/** Realtime event pushed when someone sets or clears their custom status. Empty text+emoji = cleared. */
export interface CustomStatusChangedEvent {
  userId: string;
  emoji: string;
  text: string;
  expiresAt: string | null;
}
