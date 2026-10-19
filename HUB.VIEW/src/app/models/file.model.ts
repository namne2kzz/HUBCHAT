/** Mirrors HUB.Media.Application.Files.DTOs.UploadTicketDto. */
export interface UploadTicketDto {
  fileId: string;
  uploadUrl: string;
  storageKey: string;
  expiresInSeconds: number;
}

/** Mirrors HUB.Media.Application.Files.DTOs.DownloadUrlDto. */
export interface DownloadUrlDto {
  url: string;
  expiresInSeconds: number;
}

/** Body for POST /files (CreateUploadTicket). */
export interface CreateUploadTicketRequest {
  workspaceId: string;
  channelId: string | null;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

/**
 * A file the user picked and (optionally) finished uploading — the composer's working state for one
 * pending attachment (CF-5). Not a server DTO.
 */
export interface PendingAttachment {
  /** Local id for tracking before the server assigns a fileId. */
  localId: string;
  file: File;
  /** Media file id once the upload ticket is created; null while pending. */
  fileId: string | null;
  kind: import('./message.model').AttachmentKind;
  /** Object URL for an image/video preview; null for other files. */
  previewUrl: string | null;
  status: 'uploading' | 'done' | 'error';
}
