import { MessageDto, ReactionDto, ReactionGroup } from '../models/message.model';

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

/**
 * Removes a user's reaction from the matching message (no-op when it is not there — redelivery and the
 * remover's own optimistic update can both deliver the same removal).
 * @param m Message to update.
 * @param messageId Message the reaction was on.
 * @param userId User whose reaction is removed.
 * @param emoji Emoji removed.
 * @returns The same message when unchanged; a copy without the reaction otherwise.
 */
export function removeReaction(m: MessageDto, messageId: string, userId: string, emoji: string): MessageDto {
  if (m.id !== messageId) return m;
  if (!m.reactions.some(r => r.userId === userId && r.emoji === emoji)) return m;
  return { ...m, reactions: m.reactions.filter(r => !(r.userId === userId && r.emoji === emoji)) };
}

/**
 * Groups reactions into one entry per emoji, in the order each emoji first appeared.
 * @param reactions The message's reactions (one per user per emoji).
 * @param currentUserId The viewer — marks the groups they are part of.
 * @returns One group per emoji with its count.
 */
export function groupReactions(reactions: readonly ReactionDto[], currentUserId: string): ReactionGroup[] {
  const groups = new Map<string, ReactionGroup>();
  for (const r of reactions) {
    const g = groups.get(r.emoji) ?? { emoji: r.emoji, count: 0, mine: false, userIds: [] };
    if (g.userIds.includes(r.userId)) continue;
    g.userIds.push(r.userId);
    g.count++;
    g.mine ||= r.userId === currentUserId;
    groups.set(r.emoji, g);
  }
  return [...groups.values()];
}
