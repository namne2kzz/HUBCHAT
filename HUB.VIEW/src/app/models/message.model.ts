export enum MessageFormat {
  Plain    = 0,
  Markdown = 1,
}

/** Mirrors HUB.Chat.Application.Messages.DTOs.ReactionDto. */
export interface ReactionDto {
  emoji: string;
  userId: string;
}

/** Mirrors HUB.Chat.Domain.Enums.AttachmentKind. */
export enum AttachmentKind {
  File  = 0,
  Image = 1,
  Video = 2,
}

/** Mirrors HUB.Chat.Application.Messages.DTOs.AttachmentDto. */
export interface AttachmentDto {
  id: string;
  kind: AttachmentKind;
  url: string;
  name: string;
  size: number;
  mime: string;
  width: number | null;
  height: number | null;
}

/** Mirrors HUB.Chat.Application.Messages.DTOs.MessageDto (camelCased on the wire). */
export interface MessageDto {
  id: string;
  channelId: string;
  parentId: string | null;
  /** Quote-reply target (distinct from thread parent). */
  replyToId: string | null;
  /** Original message id when forwarded. */
  forwardedFromId: string | null;
  authorId: string;
  body: string;
  format: MessageFormat;
  mentions: string[];
  reactions: ReactionDto[];
  attachments: AttachmentDto[];
  editedAt: string | null;
  createdAt: string;
  /** True when the message is a deletion tombstone — render "message deleted"; body is empty. */
  isDeleted?: boolean;
  /** True when the message is pinned in its channel. */
  isPinned?: boolean;
  /** Replies in this message's thread (channel list only; 0 for replies themselves). */
  replyCount?: number;
  /** Newest reply time, for the "N replies · 2m ago" hint; null when none. */
  lastReplyAt?: string | null;
}

/** One uploaded file attached to a new message (mirrors HUB.Chat.Application...AttachmentInput). */
export interface AttachmentInput {
  fileId: string;
  kind: AttachmentKind;
  name: string;
  size: number;
  mime: string;
  width?: number | null;
  height?: number | null;
}

/** POST /api/v1/channels/{channelId}/messages body. */
export interface PostMessageRequest {
  body: string;
  format: MessageFormat;
  parentId?: string;
  mentionedUserIds?: string[];
  /** Idempotency key (UUID) for this send; the same key on a retry returns the original message. */
  clientMessageId?: string;
  /** Quote-reply target: the message this one quotes (shown as a preview above the new message). */
  replyToId?: string;
  /** Files already uploaded via the Media service to attach to this message (CF-5). */
  attachments?: AttachmentInput[];
}

/** Mirrors HUB.Chat.Application.Messages.DTOs.ChannelAttachmentDto (Files/Media panel). */
export interface ChannelAttachmentDto {
  id: string;
  messageId: string;
  authorId: string;
  kind: AttachmentKind;
  /** Media file id (resolve a presigned download URL from it). */
  fileId: string;
  name: string;
  size: number;
  mime: string;
  width: number | null;
  height: number | null;
  createdAt: string;
}

/** Backend keyset page: HUB.Chat.Application.Common.Models.CursorPage<T> — items + nextCursor only. */
export interface CursorPage<T> {
  items: T[];
  nextCursor: string | null;
}

// ── Realtime (SignalR) event payloads ──────────────────────────────────────
/** Payload pushed by realtime-service (MessageSentConsumer). Carries the full message at post time; old events only the preview. */
export interface MessageReceivedEvent {
  messageId: string;
  channelId: string;
  authorId: string;
  preview: string;
  mentions: string[];
  sentAt: string;
  /** Full body — null for events published before the contract carried it (fall back to preview). */
  body?: string | null;
  /** Body format (0 = Plain, 1 = Markdown); null on old events. */
  format?: MessageFormat | null;
  /** Thread parent for a reply; null for a top-level message. */
  parentId?: string | null;
  /** Message creation time (timeline sort key); null on old events. */
  createdAt?: string | null;
  /** Attachments (CF-5); `url` holds the Media file id. Absent on old events. */
  attachments?: RealtimeAttachment[] | null;
}

/** Attachment shape on the realtime messageReceived event (MessageSentConsumer). */
export interface RealtimeAttachment {
  id: string;
  kind: AttachmentKind;
  fileId: string;
  name: string;
  size: number;
  mime: string;
  width: number | null;
  height: number | null;
}

export interface MessageEditedEvent {
  messageId: string;
  channelId: string;
  newBody: string;
  editedAt: string;
}

export interface MessageDeletedEvent {
  messageId: string;
  channelId: string;
}

/** Typing started/stopped event pushed via SignalR. */
export interface TypingEvent {
  channelId: string;
  userId: string;
}

/** Pushed by realtime-service (ReactionAddedConsumer) when someone reacts to a message in an open channel. */
export interface ReactionAddedEvent {
  messageId: string;
  channelId: string;
  userId: string;
  emoji: string;
}

/** Pushed by realtime-service (ReactionRemovedConsumer) when someone removes their reaction. */
export type ReactionRemovedEvent = ReactionAddedEvent;

/** Reactions on a message grouped by emoji, for display as one chip per emoji. */
export interface ReactionGroup {
  emoji: string;
  /** How many users reacted with this emoji. */
  count: number;
  /** True when the current user is one of them — the chip shows as selected and a click removes it. */
  mine: boolean;
  /** Who reacted, in reaction order (for the tooltip). */
  userIds: string[];
}

/** Emitted by the message list when the viewer clicks a reaction chip or picks an emoji. */
export interface ReactionToggle {
  messageId: string;
  emoji: string;
}
