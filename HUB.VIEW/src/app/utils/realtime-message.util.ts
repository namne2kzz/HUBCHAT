import { MessageDto, MessageFormat, MessageReceivedEvent } from '../models/message.model';

/**
 * Builds a timeline MessageDto from a realtime `messageReceived` push.
 * The push carries the whole message as posted (body, format, createdAt), so the recipient sees the same
 * thing a reload would. Events from before the contract carried those fields only have the 140-char
 * preview — then the preview is shown as plain text until a reload or catch-up replaces it.
 * Reactions/attachments are empty because a just-posted message has none yet.
 * @param e The realtime event.
 * @returns A MessageDto for display.
 */
export function messageFromRealtime(e: MessageReceivedEvent): MessageDto {
  const full = e.body != null;
  return {
    id: e.messageId,
    channelId: e.channelId,
    parentId: e.parentId ?? null,
    replyToId: null,
    forwardedFromId: null,
    authorId: e.authorId,
    body: full ? e.body! : e.preview,
    format: full ? (e.format ?? MessageFormat.Markdown) : MessageFormat.Plain,
    mentions: e.mentions ?? [],
    reactions: [],
    attachments: [],
    editedAt: null,
    createdAt: e.createdAt ?? e.sentAt,
  };
}
