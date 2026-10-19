import { inject, Injectable } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { FileService } from './file.service';

interface CachedUrl { url: string; expiresAtMs: number; }

/**
 * Resolves and caches presigned download URLs for message attachments (CF-5).
 *
 * The Media service hands out short-lived (≈5 min) presigned GET URLs, so an attachment row stores only
 * the file id — the URL is fetched on demand and cached until shortly before it expires. Without this,
 * every change detection on a message with an image would hit the Media service again.
 */
@Injectable({ providedIn: 'root' })
export class AttachmentUrlService {
  private readonly files = inject(FileService);
  private readonly cache = new Map<string, CachedUrl>();
  /** In-flight requests, so concurrent renders of the same file share one HTTP call. */
  private readonly inflight = new Map<string, Observable<string>>();

  /** Refetch this many ms before the server's expiry, to avoid handing out an about-to-die URL. */
  private static readonly SKEW_MS = 30_000;

  /**
   * Returns a usable presigned download URL for a file, from cache when still fresh.
   * @param fileId Media file id (an attachment's `url` field holds this).
   * @returns Observable of the download URL ('' when it cannot be resolved).
   */
  get(fileId: string): Observable<string> {
    const cached = this.cache.get(fileId);
    if (cached && cached.expiresAtMs > Date.now()) return of(cached.url);

    const existing = this.inflight.get(fileId);
    if (existing) return existing;

    const req = this.files.getDownloadUrl(fileId).pipe(
      map(dto => {
        this.cache.set(fileId, { url: dto.url, expiresAtMs: Date.now() + dto.expiresInSeconds * 1000 - AttachmentUrlService.SKEW_MS });
        this.inflight.delete(fileId);
        return dto.url;
      }),
      catchError(() => { this.inflight.delete(fileId); return of(''); }),
      shareReplay(1),
    );
    this.inflight.set(fileId, req);
    return req;
  }
}
