import { Injectable } from '@angular/core';

/**
 * Wraps full-page navigation so the rest of the app never touches `window.location` directly.
 *
 * HUB hands off to DASHBOARD for sign-in and sign-out, which is a real page load rather than a router
 * navigation — the two apps are separate origins. Routing that through a service keeps the URL-building
 * in one place instead of repeated at each call site, and lets tests substitute it: assigning
 * `window.location.href` in a Karma run unloads the test page and kills the whole suite.
 */
@Injectable({ providedIn: 'root' })
export class NavigationService {
  /**
   * Navigates the browser to an absolute URL, leaving the Angular app.
   * @param url Destination URL.
   */
  goTo(url: string): void {
    window.location.href = url;
  }

  /** @returns The current page URL, used as the return target for a sign-in round trip. */
  currentUrl(): string {
    return window.location.href;
  }

  /** @returns The current origin, used to build return URLs for HUB's own routes. */
  origin(): string {
    return window.location.origin;
  }
}
