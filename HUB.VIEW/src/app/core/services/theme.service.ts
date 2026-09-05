import { Injectable, signal } from '@angular/core';
import { StorageService } from './storage.service';
import { StorageKeys } from '../constants/storage-keys.constant';

export type Theme = 'light' | 'dark';

/**
 * Manages the app theme.
 *
 * The theme is applied as `data-theme="light|dark"` on <html>; every colour in the
 * app resolves from the CSS custom properties declared in `src/styles/theme.css`,
 * so switching themes never requires touching component styles.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly storage = new StorageService();

  /** Current theme. Read it in templates/components instead of touching the DOM. */
  readonly theme = signal<Theme>(this.resolveInitialTheme());

  constructor() {
    this.applyTheme(this.theme());
  }

  /** Switches to an explicit theme and persists the choice. */
  set(theme: Theme): void {
    this.theme.set(theme);
    this.storage.set(StorageKeys.theme, theme);
    this.applyTheme(theme);
  }

  /** Toggles between light and dark theme. */
  toggle(): void {
    this.set(this.theme() === 'dark' ? 'light' : 'dark');
  }

  /** Stored choice wins; otherwise follow the OS preference; otherwise light. */
  private resolveInitialTheme(): Theme {
    const stored = this.storage.get<Theme>(StorageKeys.theme);
    if (stored === 'light' || stored === 'dark') return stored;
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  private applyTheme(theme: Theme): void {
    const root = document.documentElement;
    root.setAttribute('data-theme', theme);
    // Kept in sync for Tailwind's `dark:` variant and any legacy class selectors.
    root.classList.toggle('dark', theme === 'dark');
    root.classList.toggle('light', theme === 'light');
  }
}
