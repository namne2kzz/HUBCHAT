import { collectMentionIds, filterMentionCandidates, findMentionQuery } from './mention.util';
import { DirectoryUser } from '../models/directory.model';

/** Covers the pure @mention logic behind the composer's suggestion list. */
describe('mention.util', () => {
  const user = (id: string, name: string, email = `${id}@corp.io`, isDeleted = false) =>
    ({ id, name, email, avatarClass: '', isGlobalAdmin: false, isDeleted } as DirectoryUser);

  describe('findMentionQuery', () => {
    it('detects an @word right before the caret', () => {
      expect(findMentionQuery('hi @nam', 7)).toEqual({ start: 3, query: 'nam' });
    });

    it('opens on a bare @ so the full list shows', () => {
      expect(findMentionQuery('@', 1)).toEqual({ start: 0, query: '' });
    });

    it('ignores an @ inside an email address', () => {
      expect(findMentionQuery('mail a@corp', 11)).toBeNull();
    });

    it('closes once a space ends the word', () => {
      expect(findMentionQuery('@nam ', 5)).toBeNull();
    });

    it('looks only at text before the caret', () => {
      expect(findMentionQuery('@nam rest', 4)).toEqual({ start: 0, query: 'nam' });
    });
  });

  describe('filterMentionCandidates', () => {
    const users = [user('1', 'Nam Huynh'), user('2', 'An Tran', 'antran@corp.io'), user('3', 'Old Account', 'x@y', true)];

    it('matches any part of the name, case-insensitively', () => {
      expect(filterMentionCandidates(users, 'huy').map(u => u.id)).toEqual(['1']);
    });

    it('matches the start of the email', () => {
      expect(filterMentionCandidates(users, 'antr').map(u => u.id)).toEqual(['2']);
    });

    it('never suggests deleted users', () => {
      expect(filterMentionCandidates(users, '').map(u => u.id)).toEqual(['1', '2']);
    });

    it('caps the list', () => {
      const many = Array.from({ length: 10 }, (_, i) => user(String(i), `User ${i}`));
      expect(filterMentionCandidates(many, 'user', 6).length).toBe(6);
    });
  });

  describe('collectMentionIds', () => {
    it('keeps picked users whose @name is still in the body', () => {
      const picked = [{ id: '1', name: 'Nam Huynh' }, { id: '2', name: 'An Tran' }];
      // An Tran was picked, then their @name deleted — no longer a mention.
      expect(collectMentionIds('hey @Nam Huynh check this', picked)).toEqual(['1']);
    });

    it('dedupes a user picked twice', () => {
      const picked = [{ id: '1', name: 'Nam' }, { id: '1', name: 'Nam' }];
      expect(collectMentionIds('@Nam @Nam', picked)).toEqual(['1']);
    });
  });
});
