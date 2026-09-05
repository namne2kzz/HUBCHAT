# `.claude/` — HUB AI workspace

Cấu hình, agents, skills và business docs để Claude sinh code đúng chuẩn cho **HUB** (chat/meeting microservices, add-on của DASHBOARD). Clone & adapt từ `.claude` của DASHBOARD cho tech stack + business của HUB.

## Cấu trúc
```
.claude/
├── config.json            # stack, model, project paths của HUB
├── settings.json          # hooks (pre/post-write PowerShell)
├── mcp.json.example        # MCP servers gợi ý (postgres, redis, rabbitmq, github, roslyn)
├── agents/                 # auto · dotnet-coder · architect · reviewer · db-optimizer · security-auditor · build-error-resolver
├── commands/               # /build-feature · /fix-bug · /pr-review · /tdd · /security-scan · /health-check · /deploy-docker
├── skills/                 # patterns + code templates (.NET/DDD/CQRS/EF-postgres/messaging/signalr/minio/...)
├── histories/              # business docs per feature (.dod.md) + RULES + domain-business
├── hooks/                  # pre-gen / post-gen / validation checklist + scripts
├── memory/                 # mistakes.md (lỗi cần tránh) · patterns.md (pattern nên dùng)
└── tools/pipelines/        # github-actions.yml
```

## Cách dùng
- Mọi yêu cầu code → workflow tự động trong `CLAUDE.md` (đọc `agents/auto.md` → detect skills → generate → post-gen review → update business doc).
- Trước khi sinh code: đọc `memory/mistakes.md` + `memory/patterns.md`.
- Sau khi đổi business: update `histories/{feature}.dod.md` theo `histories/RULES.md`.

## Khác biệt so với DASHBOARD
- Microservices (không monolith); messaging **RabbitMQ/MassTransit** (không Azure Service Bus); realtime **SignalR + Redis backplane**; storage **MinIO**; deploy **Docker** (không Azure/AKS); DB chỉ **PostgreSQL** (bỏ SQL Server).
- Angular assets tạm bỏ — thêm khi scaffold `HUB.VIEW`.
