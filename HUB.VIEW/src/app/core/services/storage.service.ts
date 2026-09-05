import { Injectable } from '@angular/core';

/** Thin wrapper around localStorage with typed get/set helpers. */
@Injectable({ providedIn: 'root' })
export class StorageService {
  /** Stores a value serialised as JSON. @param key Storage key. @param value Value to store. */
  set<T>(key: string, value: T): void {
    try {
      localStorage.setItem(key, JSON.stringify(value));
    } catch {
      console.warn(`StorageService: failed to set "${key}"`);
    }
  }

  /** Retrieves and deserialises a stored value. @param key Storage key. @returns Parsed value or null. */
  get<T>(key: string): T | null {
    try {
      const raw = localStorage.getItem(key);
      return raw ? (JSON.parse(raw) as T) : null;
    } catch {
      return null;
    }
  }

  /**
   * Stores a raw string without JSON serialisation — pairs with {@link getString}.
   * Use this for values read back raw (JWTs, ids); `set()` would wrap them in quotes.
   * @param key Storage key.
   * @param value Raw string to store.
   */
  setString(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      console.warn(`StorageService: failed to set "${key}"`);
    }
  }

  /** Retrieves a raw string value. @param key Storage key. @returns String or null. */
  getString(key: string): string | null {
    return localStorage.getItem(key);
  }

  /** Returns true if the key exists and is non-empty. @param key Storage key. */
  has(key: string): boolean {
    const v = localStorage.getItem(key);
    return v !== null && v !== '';
  }

  /** Removes a key from storage. @param key Storage key. */
  remove(key: string): void {
    localStorage.removeItem(key);
  }

  /** Clears all keys from storage. */
  clear(): void {
    localStorage.clear();
  }
}
