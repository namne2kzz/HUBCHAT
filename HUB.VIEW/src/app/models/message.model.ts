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
}

/** POST /api/v1/channels/{channelId}/messages body. */
export interface PostMessageRequest {
  body: string;
  format: MessageFormat;
  parentId?: string;
  mentionedUserIds?: string[];
  /** Idempotency key (UUID) for this send; the same key on a retry returns the original message. */
  clientMessageId?: string;
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
}

export interface MessageEditedEvent {
  messageId: string;
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
