import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, switchMap } from 'rxjs';
import { ConfigService } from '../core/services/config.service';
import { CreateUploadTicketRequest, FileMetaDto, FileTicketDto } from '../models/file.model';

@Injectable({ providedIn: 'root' })
export class FileService {
  private readonly http   = inject(HttpClient);
  private readonly config = inject(ConfigService);
  private get apiUrl() { return `${this.config.apiBaseUrl}/files`; }

  /**
   * 2-phase upload: get presigned PUT URL from media-service, then PUT directly to MinIO.
   * @param file File to upload.
   * @returns Observable of the completed FileMetaDto.
   */
  upload(file: File): Observable<FileMetaDto> {
    const ticket: CreateUploadTicketRequest = {
      fileName:    file.name,
      contentType: file.type || 'application/octet-stream',
      sizeBytes:   file.size,
    };
    return this.http.post<FileTicketDto>(this.apiUrl, ticket).pipe(
      switchMap(t =>
        new Observable<FileMetaDto>(observer => {
          fetch(t.uploadUrl, {
            method:  'PUT',
            body:    file,
            headers: { 'Content-Type': file.type || 'application/octet-stream' },
          })
            .then(() =>
              this.http
                .post<FileMetaDto>(`${this.apiUrl}/${t.fileId}/complete`, {})
                .subscribe({ next: m => { observer.next(m); observer.complete(); }, error: e => observer.error(e) })
            )
            .catch(e => observer.error(e));
        })
      )
    );
  }

  /** Gets download URL for a file. @param id File id. */
  getMeta(id: string): Observable<FileMetaDto> {
    return this.http.get<FileMetaDto>(`${this.apiUrl}/${id}`);
  }
}
