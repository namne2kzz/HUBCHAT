import { MessageDto } from '../models/message.model';

/**
 * How incoming messages treat one already in the list with the same id.
 * - `replace`: the incoming copy is authoritative (a full DTO from the API) — it overwrites.
 * - `keep`: the incoming copy may be partial (a realtime stub built from a 140-char preview) — the
 *   existing one wins, and the stub is only inserted when the message is not there yet.
 */
export type MergeMode = 'replace' | 'keep';

/**
 * Merges messages into a channel timeline: dedupes by id and keeps chronological order (createdAt, then id).
 * Every path that adds messages — POST response, realtime push, load-more, reconnect catch-up — goes
 * through here, so a message delivered twice by two of them shows once, and a full DTO always replaces
 * the realtime stub of the same message.
 * @param current Current timeline, oldest first.
 * @param incoming Messages to merge, any order.
 * @param mode Whether incoming copies overwrite existing ones.
 * @returns A new timeline, oldest first; `current` itself when nothing changed.
 */
export function mergeMessages(current: MessageDto[], incoming: MessageDto[], mode: MergeMode): MessageDto[] {
  if (incoming.length === 0) return current;

  const byId = new Map(current.map(m => [m.id, m] as const));
  let changed = false;
  for (const m of incoming) {
    if (byId.has(m.id) && mode === 'keep') continue;
    byId.set(m.id, m);
    changed = true;
  }
  if (!changed) return current;

  return [...byId.values()].sort(compareChronological);
}

/** Orders by createdAt, then id as a deterministic tie-break (equal timestamps are rare; exact server uuid order is not reproduced). */
function compareChronological(a: MessageDto, b: MessageDto): number {
  const t = Date.parse(a.createdAt) - Date.parse(b.createdAt);
  if (t !== 0) return t;
  return a.id < b.id ? -1 : a.id > b.id ? 1 : 0;
}
