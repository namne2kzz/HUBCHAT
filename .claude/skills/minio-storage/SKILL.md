---
name: minio-storage
description: Use when storing/serving file attachments in HUB via MinIO (S3-compatible) — presigned upload/download, metadata-only backend, IObjectStorage abstraction.
---

# Skill: MinIO Object Storage (S3-compatible)

File **không đi qua backend**: client PUT thẳng lên MinIO qua presigned URL; backend chỉ giữ metadata + phát `FileUploaded`.

## Luồng
```
POST /api/v1/files {fileName, contentType, size} → validate → StorageKey → presigned PUT (5')
client PUT file → MinIO
POST message {fileId}
media-service (async): thumbnail + virus-scan → publish FileUploaded
GET /api/v1/files/{id} → presigned GET (hạn giờ, check quyền kênh)
```

## Abstraction (đổi sang S3/Azure sau không sửa domain)
```csharp
public interface IObjectStorage
{
    Task<Uri> CreatePresignedPutAsync(string bucket, string key, string contentType, TimeSpan ttl, CancellationToken ct);
    Task<Uri> CreatePresignedGetAsync(string bucket, string key, TimeSpan ttl, CancellationToken ct);
}
```
Implement bằng `Minio` SDK hoặc `AWSSDK.S3` (MinIO nói giao thức S3).

## Rule
- Bucket-per-workspace (`ws-{workspaceId}`), key `channels/{channelId}/{fileId}`.
- Whitelist content-type, max size (vd 100MB), rate-limit upload.
- `scan_status` chặn tải về khi chưa scan xong; ảnh sinh thumbnail để preview.

## Đừng
- ❌ Proxy file bytes qua backend. ❌ Public bucket. ❌ Lưu URL cố định (dùng presigned có hạn).
