import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, from, map, switchMap } from 'rxjs';
import { ConfigService } from '../core/services/config.service';
import { CreateUploadTicketRequest, DownloadUrlDto, UploadTicketDto } from '../models/file.model';
import { skipErrorToast } from '../core/http/http-context';

/**
 * Talks to the Media service's 2-phase upload (CF-5): reserve a presigned PUT URL, upload the bytes
 * straight to MinIO, then confirm. Download URLs are presigned and short-lived, so they are fetched
 * on demand (see {@link AttachmentUrlService}), never stored.
 */
@Injectable({ providedIn: 'root' })
export class FileService {
  private readonly http   = inject(HttpClient);
  private readonly config = inject(ConfigService);
  private get apiUrl() { return `${this.config.apiBaseUrl}/files`; }

  /**
   * Uploads a file: ticket → PUT to MinIO → complete. Resolves with the Media file id.
   * @param file The file to upload.
   * @param workspaceId Owning workspace (repo) id.
   * @param channelId Channel the file is shared in.
   * @returns Observable of the uploaded file id.
   */
  upload(file: File, workspaceId: string, channelId: string): Observable<string> {
    const body: CreateUploadTicketRequest = {
      workspaceId,
      channelId,
      fileName:    file.name,
      contentType: file.type || 'application/octet-stream',
      sizeBytes:   file.size,
    };
    return this.http.post<UploadTicketDto>(this.apiUrl, body).pipe(
      switchMap(ticket =>
        from(fetch(ticket.uploadUrl, {
          method:  'PUT',
          body:    file,
          headers: { 'Content-Type': file.type || 'application/octet-stream' },
        })).pipe(
          switchMap(res => {
            if (!res.ok) throw new Error(`Upload failed (${res.status})`);
            return this.http.post<void>(`${this.apiUrl}/${ticket.fileId}/complete`, {});
          }),
          map(() => ticket.fileId),
        )),
    );
  }

  /**
   * Gets a presigned download URL for a file. Skips the global error toast — callers degrade gracefully
   * (a missing/scanning file just doesn't render).
   * @param fileId Media file id.
   * @returns Observable of the download URL DTO.
   */
  getDownloadUrl(fileId: string): Observable<DownloadUrlDto> {
    return this.http.get<DownloadUrlDto>(`${this.apiUrl}/${fileId}`, { context: skipErrorToast() });
  }
}
