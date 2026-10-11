import { applyReaction, groupReactions, removeReaction } from './reaction.util';
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

  describe('removeReaction', () => {
    it('removes only that user\'s reaction with that emoji', () => {
      const m = msg('m1', [{ emoji: ':+1:', userId: 'u1' }, { emoji: ':+1:', userId: 'u2' }]);
      expect(removeReaction(m, 'm1', 'u2', ':+1:').reactions).toEqual([{ emoji: ':+1:', userId: 'u1' }]);
    });

    it('is a no-op (same reference) when the reaction is not there', () => {
      const m = msg('m1', [{ emoji: ':+1:', userId: 'u1' }]);
      expect(removeReaction(m, 'm1', 'u9', ':+1:')).toBe(m);
      expect(removeReaction(m, 'other', 'u1', ':+1:')).toBe(m);
    });
  });

  describe('groupReactions', () => {
    it('makes one group per emoji with a count, in first-seen order', () => {
      const groups = groupReactions([
        { emoji: '👍', userId: 'a' }, { emoji: '🎉', userId: 'b' }, { emoji: '👍', userId: 'c' },
      ], 'z');
      // One "👍 2" chip, not two "👍" chips (and no duplicate track key).
      expect(groups.map(g => [g.emoji, g.count])).toEqual([['👍', 2], ['🎉', 1]]);
    });

    it('marks the groups the viewer is part of', () => {
      const groups = groupReactions([{ emoji: '👍', userId: 'me' }, { emoji: '🎉', userId: 'b' }], 'me');
      expect(groups.map(g => g.mine)).toEqual([true, false]);
    });

    it('does not double-count a duplicated reaction', () => {
      expect(groupReactions([{ emoji: '👍', userId: 'a' }, { emoji: '👍', userId: 'a' }], 'z')[0].count).toBe(1);
    });
  });
});
