import { extractWorkItemKeys, workItemUrl } from './work-item-link.util';

/**
 * Covers work-item link detection: it finds keys in DASHBOARD board URLs, uppercases and de-duplicates
 * them, caps the count, and ignores anything that is not a work-item link.
 */
describe('work-item-link util', () => {
  const DASH = 'https://dash.example.com';

  it('extracts the key from a board URL', () => {
    const body = `See ${DASH}/NFLOW/boards/NFLOW-142 for details`;
    expect(extractWorkItemKeys(body, DASH)).toEqual(['NFLOW-142']);
  });

  it('uppercases the key and de-duplicates repeats', () => {
    const body = `${DASH}/x/boards/abc-1 and again ${DASH}/x/boards/ABC-1`;
    expect(extractWorkItemKeys(body, DASH)).toEqual(['ABC-1']);
  });

  it('finds multiple distinct keys', () => {
    const body = `${DASH}/x/boards/NF-1 ${DASH}/y/boards/NF-2`;
    expect(extractWorkItemKeys(body, DASH)).toEqual(['NF-1', 'NF-2']);
  });

  it('caps the number of keys', () => {
    const body = [1, 2, 3, 4, 5].map(n => `${DASH}/x/boards/NF-${n}`).join(' ');
    expect(extractWorkItemKeys(body, DASH, 2).length).toBe(2);
  });

  it('ignores non-work-item links and other hosts', () => {
    expect(extractWorkItemKeys(`${DASH}/NFLOW/overview`, DASH)).toEqual([]);
    expect(extractWorkItemKeys('https://other.com/x/boards/NF-9', DASH)).toEqual([]);
    expect(extractWorkItemKeys('just text', DASH)).toEqual([]);
  });

  it('returns nothing when the dashboard url is empty', () => {
    expect(extractWorkItemKeys(`${DASH}/x/boards/NF-1`, '')).toEqual([]);
  });

  it('builds the deep link back to DASHBOARD', () => {
    expect(workItemUrl(DASH + '/', 'NFLOW', 'NFLOW-142')).toBe(`${DASH}/NFLOW/boards/NFLOW-142`);
  });
});
