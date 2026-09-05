import { Injectable } from '@angular/core';

export interface AppConfig {
  apiBaseUrl: string;
  hubUrl:     string;
  /** DASHBOARD frontend URL — HUB redirects unauthenticated users here. */
  dashboardUrl: string;
}

/**
 * Holds runtime configuration loaded from `/config.json` at app bootstrap.
 * Prefer this over `environment.ts` for any value that changes per deployment
 * (ports, URLs) — Docker can mount a different config.json without rebuilding the image.
 */
@Injectable({ providedIn: 'root' })
export class ConfigService {
  private config!: AppConfig;

  /**
   * Fetches `/config.json` and stores the result.
   * Called by `APP_INITIALIZER` before any component renders.
   * @returns Promise that resolves when config is loaded.
   */
  async load(): Promise<void> {
    const res = await fetch('/config.json');
    if (!res.ok) throw new Error(`Failed to load config.json (${res.status})`);
    this.config = await res.json() as AppConfig;
  }

  /** @returns The full API base URL (without trailing slash). */
  get apiBaseUrl(): string { return this.config.apiBaseUrl.replace(/\/$/, ''); }

  /** @returns The SignalR hub endpoint URL. */
  get hubUrl(): string { return this.config.hubUrl; }

  /** @returns The DASHBOARD frontend origin URL. */
  get dashboardUrl(): string { return this.config.dashboardUrl.replace(/\/$/, ''); }
}
