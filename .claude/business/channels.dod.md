# Channels — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-08-02 | 13:38 | P3 linked thread | Channel có thể link tới DASHBOARD resource (work item): OpenLinkedThread find-or-create, LinkType/LinkExternalId/Key/Url |
| 2026-08-01 | 20:18 | Khởi tạo document | Tạo doc business ban đầu cho feature Channels (P1) |

---

## Purpose
Kênh chat trong 1 workspace (= Repository/Project của DASHBOARD) để team trao đổi theo chủ đề, hoặc nhắn riêng (DM).

## Key Entities & Relationships
- `Channel` (aggregate root): `WorkspaceId` (= dashboard_repo_id), `Name`, `Slug`, `Type`, `Topic`, `IsPrivate`, `IsArchived`, `CreatedBy`, `Members`.
- `ChannelMember` (trong aggregate): `UserId`, `Role` (`ChannelMemberRole`), `Muted`, `LastReadAt`, `JoinedAt`.
- `ChannelType`: Public(0) · Private(1) · Dm(2) · GroupDm(3).

## Business Rules & Invariants
- Tên kênh bắt buộc (không rỗng); slug tự sinh từ tên (lowercase, non-alnum → '-').
- `Type` Private/Dm/GroupDm ⇒ `IsPrivate = true`.
- Người tạo tự động là **Owner** (`ChannelMemberRole.Owner`).
- Không thêm trùng member (1 user 1 lần trong 1 kênh) — `AddMember` throw `DomainException` nếu đã có.
- Kênh `IsArchived` ⇒ read-only: `EnsureWritable()` chặn post message.
- Unique `(WorkspaceId, Slug)`.

## Main Workflows
1. **Create**: `POST /api/v1/channels` (WorkspaceId, Name, Type, Topic) → tạo kênh + creator=Owner.
2. **List**: `GET /api/v1/channels?workspaceId=` → kênh Public hoặc kênh user là member.
3. **Join**: `POST /api/v1/channels/{id}/members` → thêm caller làm Member (idempotent).
4. **Mark read**: `POST /api/v1/channels/{id}/read` → cập nhật `LastReadAt` của member.

## Definition of Done
- [ ] Invariants enforce trong aggregate (không rải ra controller).
- [ ] Query list dùng `AsNoTracking()` + `Select()`, scope theo workspace + visibility.
- [ ] Unique (WorkspaceId, Slug) + index (WorkspaceId).
- [ ] Unit test: create→owner, private→isPrivate, empty name→throw, add dup→throw, archived→block.

## Edge Cases & Notes
- DM/GroupDm dùng chung `Channel` (Type khác) — DM đúng 2 member là rule ở tầng tạo (P2 hoàn thiện).
- Membership kênh (HUB) khác membership workspace (pull từ DASHBOARD): vào được workspace mới thấy/join kênh.

## Linked discussion threads (P3)
- Channel có thể là **thread thảo luận** gắn 1 DASHBOARD resource: `LinkType` (WorkItem/WikiPage), `LinkExternalId`, `LinkExternalKey` (vd "DASH-142"), `LinkUrl` (deep-link về DASHBOARD).
- `POST /api/v1/channels/linked` (OpenLinkedThread): **find-or-create** theo `(WorkspaceId, LinkExternalId)` — nếu đã có thì trả kênh cũ + thêm caller làm member; chưa có thì tạo kênh Public linked.
- Deep-link 2 chiều: DASHBOARD → HUB bằng channelId; HUB → DASHBOARD bằng `LinkUrl`.
- Index `(WorkspaceId, LinkExternalId)` cho find-or-create.
