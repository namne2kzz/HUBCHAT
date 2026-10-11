import { DirectoryUser } from '../models/directory.model';

/** An `@query` being typed: where the `@` sits and the text after it. */
export interface MentionQuery {
  /** Index of the `@` in the text. */
  start: number;
  /** Text typed after the `@` (may be empty). */
  query: string;
}

/** A user picked from the mention list: id + the exact name inserted as `@name`. */
export interface PickedMention {
  id: string;
  name: string;
}

/**
 * Finds the `@mention` being typed immediately before the caret.
 * Only triggers at the start of the text or after whitespace, so emails (`a@b.com`) do not open the list.
 * @param text Composer text.
 * @param caret Caret position.
 * @returns The active query, or null when the caret is not right after an `@word`.
 */
export function findMentionQuery(text: string, caret: number): MentionQuery | null {
  const m = /(^|\s)@([^\s@]{0,30})$/u.exec(text.slice(0, caret));
  if (!m) return null;
  return { start: caret - m[2].length - 1, query: m[2] };
}

/**
 * Candidates matching a query: by any part of the name, or the start of the email. Deleted users are left out.
 * @param users All mentionable users.
 * @param query Text typed after `@`.
 * @param limit Maximum suggestions.
 * @returns Matching users, in input order.
 */
export function filterMentionCandidates(users: readonly DirectoryUser[], query: string, limit = 6): DirectoryUser[] {
  const q = query.toLowerCase();
  return users
    .filter(u => !u.isDeleted && (u.name.toLowerCase().includes(q) || u.email.toLowerCase().startsWith(q)))
    .slice(0, limit);
}

/**
 * Ids to send as `mentionedUserIds`: users who were picked AND whose `@name` is still in the body —
 * deleting the text un-mentions them.
 * @param body Final message body.
 * @param picked Users picked while composing.
 * @returns Distinct user ids.
 */
export function collectMentionIds(body: string, picked: readonly PickedMention[]): string[] {
  return [...new Set(picked.filter(p => body.includes('@' + p.name)).map(p => p.id))];
}
