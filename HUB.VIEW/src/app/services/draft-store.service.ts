import { Injectable } from '@angular/core';

/**
 * Per-channel composer drafts, persisted in localStorage (CF-17).
 *
 * Drafts are a convenience, not data we own: they live only in the one browser that typed them and
 * never sync. That is why this is localStorage and not a server round-trip — a half-typed message is
 * cheap to lose and expensive to be wrong about (showing someone else's draft, or a stale one after
 * send). Every access is wrapped because storage can be absent or throw (private windows, blocked
 * site data), and the composer must work regardless.
 */
@Injectable({ providedIn: 'root' })
export class DraftStoreService {
  private static readonly PREFIX = 'hub.draft.';

  /** Reads the saved draft for a channel. @param channelId Channel id. @returns The draft, or '' when none. */
  get(channelId: string): string {
    if (!channelId) return '';
    try {
      return localStorage.getItem(DraftStoreService.PREFIX + channelId) ?? '';
    } catch {
      return '';
    }
  }

  /**
   * Saves (or clears) the draft for a channel. An empty/whitespace body removes the key so the
   * channel list doesn't show a phantom "draft" badge for a message that was sent or wiped.
   * @param channelId Channel id.
   * @param body Current composer text.
   */
  set(channelId: string, body: string): void {
    if (!channelId) return;
    const key = DraftStoreService.PREFIX + channelId;
    try {
      if (body.trim().length === 0) localStorage.removeItem(key);
      else localStorage.setItem(key, body);
    } catch {
      /* storage unavailable — drafts are best-effort */
    }
  }

  /** Clears a channel's draft (called on send). @param channelId Channel id. */
  clear(channelId: string): void {
    this.set(channelId, '');
  }

  /** True when a channel has a non-empty saved draft. @param channelId Channel id. @returns Whether a draft exists. */
  has(channelId: string): boolean {
    return this.get(channelId).trim().length > 0;
  }
}
