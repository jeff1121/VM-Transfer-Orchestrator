# Copilot Instructions — VMTO (VM Transfer Orchestrator)

## Build, Test, Lint

```bash
# Build
dotnet build VMTO.sln

# Test all
dotnet test VMTO.sln

# Test a single project
dotnet test tests/VMTO.Domain.Tests

# Test a single test by filter
dotnet test tests/VMTO.Domain.Tests --filter "FullyQualifiedName~MigrationJobTests"

# Frontend
cd frontend && npm install && npm run dev      # dev server
cd frontend && npm run build                    # production build
cd frontend && npm run type-check               # TypeScript check

# Infrastructure (Docker Compose)
cd infra && cp .env.example .env && docker compose up -d
```

## Architecture

Clean Architecture + DDD with event-driven orchestration. Six .NET projects + Vue3 frontend:

```
VMTO.Shared → Result types, error codes, correlation, telemetry constants
VMTO.Domain → Aggregates, value objects, domain events, strategy interfaces (ZERO framework deps)
VMTO.Application → Commands/queries, ports (repository + service interfaces), DTOs
VMTO.Infrastructure → EF Core, Redis, S3/MinIO, vSphere/PVE clients, crypto, SignalR, telemetry
VMTO.API → ASP.NET Core Minimal API + SignalR hub + Swagger + Hangfire dashboard
VMTO.Worker → MassTransit consumers (step handlers) + saga state machine
VMTO.LicenseServer → Optional standalone license service
```

**Dependency flow** (strict — never reverse):
`Domain → Shared` · `Application → Domain` · `Infrastructure → Application` · `API/Worker → Infrastructure`

**Orchestration split:**
- **MassTransit + RabbitMQ** — saga-based migration step orchestration (ExportVmdk → ConvertDisk → Upload → ImportToPve → Verify)
- **Hangfire** — scheduled/recurring jobs (artifact cleanup, incremental sync schedules)

**State machines:**
- Job: `Created → Queued → Running → Pausing → Paused → Resuming → Cancelling → Cancelled → Failed → Succeeded`
- Step: `Pending → Running → Retrying → Failed → Skipped → Succeeded`

State transitions are enforced in the `MigrationJob` aggregate with invariant checks returning `Result` types.

## Key Conventions

### Domain Layer Rules
- Domain project must NEVER reference ASP.NET Core, EF Core, MassTransit, or any framework
- Aggregates enforce invariants — not anemic models. State transitions happen via methods (`job.Enqueue()`, `job.Start()`, etc.)
- Domain events are raised inside aggregates and collected via `DomainEvents` property
- Use `Result`/`Result<T>` from VMTO.Shared for all operations that can fail

### Application Layer
- Ports pattern: interfaces in `Application/Ports/`, implementations in `Infrastructure/`
- Commands and queries are records. Handler interfaces: `ICommandHandler<T>`, `IQueryHandler<T, TResult>`
- DTOs never expose secrets (ConnectionDto omits encrypted secret)

### Infrastructure
- EF Core entity configs use Fluent API with snake_case table names
- `MockVSphereClient` and `MockPveClient` exist for mock mode (set `MockMode=true` in config)
- All external process calls (qemu-img) must have: timeout, stderr capture, exit code handling, CancellationToken
- Secrets encrypted at rest via DataProtection (`IEncryptionService`); key provider interface supports Vault/KMS

### Worker Consumers
- Each MassTransit consumer follows: load job/step → mark Running → do work → mark Succeeded/Failed → publish saga message
- All long operations must propagate `CancellationToken`
- Workers are stateless and horizontally scalable (competing consumers)
- Progress reported via `INotificationService` (SignalR)

### Frontend
- Vue3 + TypeScript + Vite with Pinia stores
- API client in `frontend/src/api/` with Axios + auto correlation ID headers
- SignalR composable in `frontend/src/composables/useSignalR.ts`
- Proxy config in `vite.config.ts`: `/api` and `/hubs` → backend

### Naming & Language
- Namespace root: `VMTO.*`
- README and docs in 繁體中文; code and API naming in English
- File-scoped namespaces throughout
- `TreatWarningsAsErrors=true` in Directory.Build.props

### Deployment
- `infra/docker-compose.yml` — full dev stack (PostgreSQL, Redis, RabbitMQ, MinIO, API, Worker, Frontend)
- `infra/.env.example` — all configurable values
- `helm/` — Kubernetes deployment with dev/prod value files
- Audit logs are append-only (no update/delete)
