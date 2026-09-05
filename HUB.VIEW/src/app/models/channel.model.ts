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
  /** Local-only: incremented by shell on incoming realtime message. Not from server. */
  unreadCount?: number;
}

/** Mirrors HUB.Chat.Application.Channels.DTOs.ChannelMemberDto. */
export interface ChannelMemberDto {
  userId: string;
  role: ChannelMemberRole;
  muted: boolean;
  joinedAt: string;
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
