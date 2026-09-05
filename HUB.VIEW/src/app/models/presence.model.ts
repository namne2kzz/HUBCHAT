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
