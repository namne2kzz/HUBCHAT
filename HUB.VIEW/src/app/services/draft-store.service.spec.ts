import { DraftStoreService } from './draft-store.service';

/**
 * Covers DraftStoreService (CF-17): drafts are namespaced per channel, an empty body clears the key
 * (so no phantom "draft" badge after send), and every path tolerates storage being unavailable.
 */
describe('DraftStoreService', () => {
  let service: DraftStoreService;

  beforeEach(() => {
    localStorage.clear();
    service = new DraftStoreService();
  });

  afterEach(() => localStorage.clear());

  it('returns an empty string when there is no draft', () => {
    expect(service.get('c1')).toBe('');
    expect(service.has('c1')).toBeFalse();
  });

  it('saves and reads a draft per channel', () => {
    service.set('c1', 'hello');
    service.set('c2', 'world');

    expect(service.get('c1')).toBe('hello');
    expect(service.get('c2')).toBe('world');
    expect(service.has('c1')).toBeTrue();
  });

  it('clears the key when the body is empty or whitespace', () => {
    service.set('c1', 'draft');
    service.set('c1', '   ');

    expect(service.get('c1')).toBe('');
    expect(service.has('c1')).toBeFalse();
    expect(localStorage.getItem('hub.draft.c1')).toBeNull();
  });

  it('clear() removes the draft', () => {
    service.set('c1', 'draft');
    service.clear('c1');
    expect(service.get('c1')).toBe('');
  });

  it('ignores an empty channel id', () => {
    service.set('', 'nope');
    expect(service.get('')).toBe('');
  });

  it('does not throw when localStorage is unavailable', () => {
    const original = Object.getOwnPropertyDescriptor(window, 'localStorage');
    Object.defineProperty(window, 'localStorage', {
      configurable: true,
      get() { throw new Error('blocked'); },
    });

    expect(() => service.set('c1', 'x')).not.toThrow();
    expect(service.get('c1')).toBe('');

    if (original) Object.defineProperty(window, 'localStorage', original);
  });
});
