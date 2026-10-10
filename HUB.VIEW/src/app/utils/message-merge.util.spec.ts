import { mergeMessages } from './message-merge.util';
import { MessageDto } from '../models/message.model';

/**
 * Covers mergeMessages: a message reaching the timeline by two paths (POST response + realtime push, or
 * realtime + reconnect catch-up) must appear once, in order, and as its full version — never the stub.
 */
describe('mergeMessages', () => {
  const msg = (id: string, minute: number, body = id) =>
    ({ id, body, createdAt: `2026-10-08T09:${String(minute).padStart(2, '0')}:00Z` } as MessageDto);

  it('appends new messages in chronological order', () => {
    const result = mergeMessages([msg('a', 1)], [msg('c', 3), msg('b', 2)], 'replace');
    expect(result.map(m => m.id)).toEqual(['a', 'b', 'c']);
  });

  it('shows a message delivered twice only once', () => {
    const result = mergeMessages([msg('a', 1)], [msg('a', 1)], 'keep');
    expect(result.length).toBe(1);
  });

  it('lets a full DTO replace the realtime stub of the same message', () => {
    const stub = msg('a', 1, 'first 140 chars…');
    const full = msg('a', 1, 'the whole message');
    expect(mergeMessages([stub], [full], 'replace')[0].body).toBe('the whole message');
  });

  it('never lets a stub overwrite the full message', () => {
    // Realtime may arrive after the POST response — the sender must keep the full text.
    const full = msg('a', 1, 'the whole message');
    const stub = msg('a', 1, 'first 140 chars…');
    expect(mergeMessages([full], [stub], 'keep')[0].body).toBe('the whole message');
  });

  it('returns the same array when nothing changed, so signals do not re-render', () => {
    const current = [msg('a', 1)];
    expect(mergeMessages(current, [msg('a', 1)], 'keep')).toBe(current);
    expect(mergeMessages(current, [], 'replace')).toBe(current);
  });

  it('breaks timestamp ties by id so the order is stable', () => {
    const result = mergeMessages([], [msg('b', 1), msg('a', 1)], 'replace');
    expect(result.map(m => m.id)).toEqual(['a', 'b']);
  });
});
