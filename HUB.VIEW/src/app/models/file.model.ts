export type FileStatus = 'Pending' | 'Uploaded' | 'Failed';

export interface FileTicketDto {
  fileId: string;
  uploadUrl: string;
  key: string;
}

export interface FileMetaDto {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: FileStatus;
  downloadUrl: string | null;
  createdAt: string;
}

export interface CreateUploadTicketRequest {
  fileName: string;
  contentType: string;
  sizeBytes: number;
}
