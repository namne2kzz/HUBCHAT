/**
 * Finds DASHBOARD work-item links in a message body so they can be unfurled into a preview card.
 *
 * A work-item URL looks like `{dashboardUrl}/{repoCode}/boards/{KEY}`, e.g.
 * `https://dashboard.example.com/NFLOW/boards/NFLOW-142`. The key (`NFLOW-142`) is what the API resolves.
 */

/** Extracts the distinct work-item keys linked in a body (max a few, to cap unfurls per message). */
export function extractWorkItemKeys(body: string, dashboardUrl: string, max = 3): string[] {
  if (!body || !dashboardUrl) return [];

  // Escape the configured base URL for use in a regex, then match "/…/boards/KEY".
  const base = dashboardUrl.replace(/\/+$/, '').replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  // KEY = CODE-NUMBER (code is letters/digits, number is digits). Stop at a slash, space, or end.
  const re = new RegExp(`${base}/[^/\\s]+/boards/([A-Za-z0-9]+-\\d+)`, 'g');

  const keys: string[] = [];
  for (const m of body.matchAll(re)) {
    const key = m[1].toUpperCase();
    if (!keys.includes(key)) keys.push(key);
    if (keys.length >= max) break;
  }
  return keys;
}

/** Builds the DASHBOARD deep-link for a work item from its context. */
export function workItemUrl(dashboardUrl: string, repoCode: string, key: string): string {
  return `${dashboardUrl.replace(/\/+$/, '')}/${repoCode}/boards/${key}`;
}
