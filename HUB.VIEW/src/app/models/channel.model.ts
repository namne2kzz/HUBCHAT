/**
 * Enums are serialised as NUMBERS by the backend (System.Text.Json default, no string converter).
 * Keep these numeric to match the wire format exactly.
 */
export enum ChannelType {
  Public  = 0,
  Private = 1,
  Dm      = 2,
  GroupDm = 3,
}

export enum ChannelMemberRole {
  Member = 0,
  Admin  = 1,
  Owner  = 2,
}

export enum LinkedResourceType {
  WorkItem = 0,
  WikiPage = 1,
}

/** Mirrors HUB.Chat.Application.Channels.DTOs.ChannelDto (camelCased on the wire). */
export interface ChannelDto {
  id: string;
  workspaceId: string;
  name: string;
  slug: string;
  type: ChannelType;
  topic: string;
  isPrivate: boolean;
  isArchived: boolean;
  memberCount: number;
  createdAt: string;
  linkType: LinkedResourceType | null;
  linkExternalKey: string | null;
  linkUrl: string;
  /** True when the acting user is a member. */
  isMember: boolean;
  /** For DMs: the other participant's user id (relative to the caller). Null for non-DM channels. */
  otherUserId?: string | null;
  /** The current user's role in this channel; null when not a member. */
  myRole?: ChannelMemberRole | null;
  /**
   * Unread top-level messages from others since the caller's read point (server-computed, capped at 100).
   * Seeds the sidebar badge on load; realtime then keeps the badge current.
   */
  unreadCount?: number;
}

/** Pushed to the reader's own sessions when they read a channel elsewhere (another tab/device). */
export interface ChannelReadEvent {
  channelId: string;
  readAt: string;
}

/** Pushed to the other DM participant when someone reads up to a point — drives "Seen" (CF-15). */
export interface ReadReceiptEvent {
  channelId: string;
  userId: string;
  readAt: string;
}

/** Mirrors HUB.Chat.Application.Channels.DTOs.ChannelMemberDto. */
export interface ChannelMemberDto {
  userId: string;
  role: ChannelMemberRole;
  muted: boolean;
  joinedAt: string;
  /** The member's read point — the DM "Seen" receipt (CF-15); null if never read. */
  lastReadAt?: string | null;
}

/** POST /api/v1/channels body. */
export interface CreateChannelRequest {
  workspaceId: string;
  name: string;
  type: ChannelType;
  topic?: string;
}

/** PATCH /api/v1/channels/{id} body. */
export interface UpdateChannelRequest {
  name?: string;
  topic?: string;
}

/** POST /api/v1/channels/{id}/members body (admin adds a user). */
export interface AddChannelMemberRequest {
  userId: string;
}

/** POST /api/v1/channels/linked body. */
export interface OpenLinkedThreadRequest {
  workspaceId: string;
  linkType: LinkedResourceType;
  externalId: string;
  externalKey: string;
  title?: string;
  url?: string;
}

/**
 * Pushed by realtime-service (ChannelMemberRemovedConsumer) to the removed user's connections: they were
 * removed from — or left — the channel, and the server has already taken them out of its realtime group.
 */
export interface ChannelAccessRevokedEvent {
  channelId: string;
  /** True when the channel was just made private (lock-down) — the client must close it, not offer "join". */
  nowPrivate?: boolean;
}
