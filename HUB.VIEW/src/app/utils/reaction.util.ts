import { MessageDto } from '../models/message.model';

/**
 * Adds a reaction to the matching message, unless that user already has that emoji on it — the reactor's
 * own UI and a broker redelivery can both deliver the same reaction twice.
 * @param m Message to update.
 * @param messageId Reacted message id.
 * @param userId Reacting user.
 * @param emoji Emoji shortcode.
 * @returns The same message when unchanged; a copy with the reaction appended otherwise.
 */
export function applyReaction(m: MessageDto, messageId: string, userId: string, emoji: string): MessageDto {
  if (m.id !== messageId) return m;
  if (m.reactions.some(r => r.userId === userId && r.emoji === emoji)) return m;
  return { ...m, reactions: [...m.reactions, { emoji, userId }] };
}
