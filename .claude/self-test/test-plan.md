# Test Plan — Self-Test UI (HUB)

Đọc `RULES.md` trước khi chạy hoặc ghi vào folder này.

## Môi trường

| Thành phần | Địa chỉ | Cách chạy |
|-----------|---------|-----------|
| UI (HUB.VIEW) | `http://localhost:4202` | `npm start` trong `HUB.VIEW/` (dev), hoặc container `hub-view` qua docker-compose |
| Gateway (YARP — entry point duy nhất) | `http://localhost:8080` | `docker compose up` |
| DASHBOARD (SSO issuer — bắt buộc phải sống) | `http://localhost:4200` / API `:5152` | chạy ở repo `C:\DEV\DASHBOARD` |
| MinIO console (kiểm tra attachment) | `http://localhost:9001` | docker-compose |
| Jaeger (trace khi debug) | `http://localhost:16686` | docker-compose |
| Browser | Microsoft Edge (`msedge`) | Playwright MCP |

**HUB không chạy độc lập được**: JWT do DASHBOARD phát hành, data pull từ DASHBOARD `/internal/v1/*`. DASHBOARD chưa sống → DỪNG, báo user, không test tiếp.

Credential + URL đọc từ **`.claude/self-test.local.json`** (gitignored):

```json
{
  "baseUrl": "http://localhost:4202",
  "gatewayUrl": "http://localhost:8080",
  "dashboardUrl": "http://localhost:4200",
  "email": "...",
  "password": "...",
  "channelName": "..."
}
```

Thiếu file hoặc thiếu field → hỏi user, KHÔNG tự đoán.

## Quy trình

Xem agent `.claude/agents/ui-tester.md` (mục "Quy trình cho MỖI module") và `RULES.md` mục 0 + 6.

## Danh sách module

| Module | Ưu tiên | Business doc | File test | Cần 2 tab (realtime) |
|--------|---------|--------------|-----------|----------------------|
| auth (SSO từ DASHBOARD) | Cao | — (xem `CLAUDE.md` + `docs/DASHBOARD-INTERNAL-API.md`) | `modules/auth.test.md` | Không |
| channels | Cao | `channels.dod.md` | `modules/channels.test.md` | Có |
| messages | Cao | `messages.dod.md` | `modules/messages.test.md` | Có |
| members | Cao | `members.dod.md` | `modules/members.test.md` | Có |
| reactions | Trung bình | `reactions.dod.md` | `modules/reactions.test.md` | Có |
| notifications | Trung bình | `notifications.dod.md` | `modules/notifications.test.md` | Có |
| presence | Trung bình | `presence.dod.md` | `modules/presence.test.md` | Có |
| media (attachment / MinIO) | Trung bình | `media.dod.md` | `modules/media.test.md` | Không |
| integration-dashboard | Trung bình | `integration-dashboard.dod.md` | `modules/integration-dashboard.test.md` | Không |
| meeting | Thấp | `meeting.dod.md` | `modules/meeting.test.md` | Có |

`/self-test` không kèm argument → chạy theo thứ tự ưu tiên (Cao → Trung bình → Thấp), bỏ qua module có trong `wip-features.md`.
