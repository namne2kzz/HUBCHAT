import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of, switchMap } from 'rxjs';
import { AttachmentDto, AttachmentKind } from '../../models/message.model';
import { AttachmentUrlService } from '../../services/attachment-url.service';

/**
 * Renders one message attachment (CF-5): an inline image/video, or a download chip for other files.
 * The presigned URL is resolved lazily from the attachment's `url` (= Media file id) and cached.
 */
@Component({
  selector: 'app-attachment-view',
  standalone: true,
  imports: [],
  templateUrl: './attachment-view.component.html',
  styleUrl: './attachment-view.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AttachmentViewComponent {
  private readonly urls = inject(AttachmentUrlService);

  readonly attachment = input.required<AttachmentDto>();
  /** Compact mode for the Files panel (smaller thumbnails). */
  readonly compact = input(false);

  readonly AttachmentKind = AttachmentKind;

  /** True when the image/video failed to load — show a fallback chip instead. */
  protected readonly loadError = signal(false);

  /** The attachment's file id (stored in `url`). */
  protected readonly fileId = computed(() => this.attachment().url);

  /** Resolved presigned download URL ('' until loaded / on failure). */
  protected readonly downloadUrl = toSignal(
    toObservable(this.fileId).pipe(switchMap(id => (id ? this.urls.get(id) : of('')))),
    { initialValue: '' },
  );

  /** Human-readable file size (e.g. "1.4 MB"). */
  protected readonly sizeLabel = computed(() => formatBytes(this.attachment().size));

  /**
   * Opens/downloads the file. For a file chip the preview URL is never rendered, so the signal may still
   * be empty — resolve it on demand, then navigate. Opened in a new tab; presigned URLs are short-lived.
   */
  protected open(): void {
    const ready = this.downloadUrl();
    if (ready) { this.navigate(ready); return; }
    this.urls.get(this.fileId()).subscribe(url => { if (url) this.navigate(url); });
  }

  private navigate(url: string): void {
    window.open(url, '_blank', 'noopener');
  }
}

/** Formats a byte count into a short human string. */
function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB'];
  let v = bytes / 1024;
  let i = 0;
  while (v >= 1024 && i < units.length - 1) { v /= 1024; i++; }
  return `${v.toFixed(v < 10 ? 1 : 0)} ${units[i]}`;
}
