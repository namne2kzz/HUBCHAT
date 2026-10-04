import { applyReaction } from './reaction.util';
import { MessageDto } from '../models/message.model';

/**
 * Covers applyReaction: the realtime reaction push must land on the right message exactly once, since the
 * reacting user and a broker redelivery can both deliver the same reaction twice.
 */
describe('applyReaction', () => {
  const msg = (id: string, reactions: { emoji: string; userId: string }[] = []) =>
    ({ id, reactions } as unknown as MessageDto);

  it('appends the reaction to the matching message', () => {
    const updated = applyReaction(msg('m1'), 'm1', 'u1', ':+1:');
    expect(updated.reactions).toEqual([{ emoji: ':+1:', userId: 'u1' }]);
  });

  it('leaves other messages untouched (same reference)', () => {
    const other = msg('m2');
    expect(applyReaction(other, 'm1', 'u1', ':+1:')).toBe(other);
  });

  it('ignores a reaction the user already has on the message', () => {
    const existing = msg('m1', [{ emoji: ':+1:', userId: 'u1' }]);
    expect(applyReaction(existing, 'm1', 'u1', ':+1:')).toBe(existing);
  });

  it('keeps the same emoji from different users separate', () => {
    const existing = msg('m1', [{ emoji: ':+1:', userId: 'u1' }]);
    expect(applyReaction(existing, 'm1', 'u2', ':+1:').reactions.length).toBe(2);
  });
});
