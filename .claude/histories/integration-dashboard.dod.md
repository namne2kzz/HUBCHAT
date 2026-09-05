# Integration với DASHBOARD — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-08-02 | 13:38 | P3 work-item context | Thêm pull `/internal/v1/work-items/{id}` (dashboard-gateway cache) để gắn ngữ cảnh thread; deep-link 2 chiều |
| 2026-08-01 | 20:18 | Khởi tạo document | Tạo doc business ban đầu cho SSO + pull data DASHBOARD |

---

## Purpose
HUB dùng chung SSO và lấy data (user directory, membership/role, work-item context) từ DASHBOARD — không đọc DB chéo, không nghe broker DASHBOARD.

## Key Entities & Relationships
- SSO: JWT **HMAC-SHA256** ký bằng secret dùng chung; claims `sub`/`uid`/`email`/`name`.
- `DirectoryUser` (read-model cache): `Id`, `DisplayName`, `Email`, `AvatarClass`, `IsGlobalAdmin`.
- Nguồn: DASHBOARD `/internal/v1/*` (service-token header `X-Internal-Token`).

## Business Rules & Invariants
- HUB là **resource server thuần**: validate token (cùng Issuer/Audience), KHÔNG login/refresh, KHÔNG thêm claim.
- Workspace HUB = **Repository/Project** DASHBOARD; user vào workspace ⟺ là `RepositoryMember`.
- Membership/role **pull `/internal/v1/me`**, cache Redis (TTL ngắn) — không nhét vào JWT (danh sách động).
- Profile cache TTL ~15'; membership TTL ~3'. DASHBOARD down → phục vụ từ cache (degraded).

## Main Workflows
1. Validate JWT ở gateway + mỗi service (`HUB.Shared.Auth`).
2. Gặp userId lạ → `dashboard-gateway` pull `/internal/v1/users/{id}` → cache.
3. Cần quyền vào workspace → pull `/internal/v1/me` → cache.

## Definition of Done
- [ ] Secret + Issuer/Audience trùng DASHBOARD (env chung).
- [ ] Service-token chỉ gọi được trong Docker network (không expose ra gateway public).
- [ ] Polly resilience (retry + circuit breaker) + fallback cache khi DASHBOARD lỗi.

## Edge Cases & Notes
- DASHBOARD cần expose nhóm endpoint read-only `/internal/v1/*` (spec `docs/DASHBOARD-INTERNAL-API.md`).
- Avatar là CSS class, không phải URL.

## Work-item context + deep-link (P3)
- `dashboard-gateway` thêm `GetWorkItemAsync` → pull `/internal/v1/work-items/{id}` (cache Redis TTL 2') → expose `GET /api/v1/directory/work-items/{id}` (WorkItemContext: Id/Key/Title/State/RepositoryId/RepositoryCode).
- chat-service tạo **linked thread** (xem channels.dod) gắn workItemId; hiển thị ngữ cảnh bằng cách gọi endpoint trên.
- DASHBOARD cần bổ sung `/internal/v1/work-items/{id}` (đã ghi trong `docs/DASHBOARD-INTERNAL-API.md`, Phase 3).
