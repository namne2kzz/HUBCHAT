# Media — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-08-02 | 12:58 | Impl P2 | media-service: MinIO presigned upload/download, metadata + FileUploaded (outbox) |
| 2026-08-01 | 00:00 | Khởi tạo document | Tạo stub ban đầu |

---

## Purpose
Gửi/lưu file đính kèm qua **MinIO** (S3-compatible). File **không đi qua backend** — client PUT thẳng lên MinIO qua presigned URL; backend giữ metadata + phát `FileUploaded`.

## Key Entities & Relationships
- `FileObject` (aggregate): `WorkspaceId`, `ChannelId?`, `FileName`, `ContentType`, `SizeBytes`, `StorageKey`, `ScanStatus`, `UploadedBy`. Bucket `ws-{workspaceId}`, key `channels/{channelId}/{fileId}`.
- `ScanStatus`: Pending(0) · Clean(1) · Infected(2).
- `IObjectStorage` → `MinioObjectStorage`. DB `hub_media`. Publish `FileUploaded` (outbox).

## Business Rules & Invariants
- Validate khi tạo ticket: size > 0 và ≤ 100MB; content-type thuộc allow-list (image/video/audio/text/application).
- Upload 2 pha: (1) tạo ticket → metadata Pending + presigned PUT (5'); (2) complete → mark Clean + publish `FileUploaded`.
- Download chỉ khi `ScanStatus == Clean` (409 nếu Pending/Infected); presigned GET có hạn 5'.
- StorageKey sinh 1 lần, không lộ file public.

## Main Workflows
1. `POST /api/v1/files` → UploadTicket (fileId + presigned PUT URL).
2. Client PUT file thẳng lên MinIO.
3. `POST /api/v1/files/{id}/complete` → mark scanned + publish `FileUploaded`.
4. `GET /api/v1/files/{id}` → presigned GET URL.

## Definition of Done
- [ ] Presigned PUT/GET hoạt động (host reachable bởi client).
- [ ] Publish `FileUploaded` qua outbox (atomic).
- [ ] Giới hạn size/type + chặn download khi chưa Clean.

## Edge Cases & Notes
- **Presigned host**: dùng `host.docker.internal:9000` để URL reachable từ cả container lẫn browser (Docker Desktop). Prod: đặt MinIO sau reverse proxy + set endpoint public.
- Virus-scan **chưa thật** (P2 giả định Clean) — cắm scan hook trước GA.
- Thumbnail ảnh/video chưa làm — backlog.
