# Tasks — VMTO (VM Transfer Orchestrator)

> Phase 1–7 已完成 ✅ | Phase 8 版本管理與審查 ✅

---

## Phase 1: Foundation (Scaffolding + Domain Core)

- [x] **1-scaffold** — Solution & Project Scaffolding
  - Create .NET solution, all VMTO.* projects, Directory.Build.props, .editorconfig, project references following Clean Architecture layering rules
  - `VMTO.Domain` → no framework dependencies
  - `VMTO.Application` → references Domain only
  - `VMTO.Infrastructure` → references Application + Domain
  - `VMTO.API` → references Application + Infrastructure
  - `VMTO.Worker` → references Application + Infrastructure
  - `VMTO.Shared` → referenced by all
  - Test projects referencing their corresponding src projects

- [x] **1-domain-core** — Domain Entities, Value Objects, Events *(depends on: 1-scaffold)*
  - `MigrationJob` aggregate root (strategy, options, status, progress, result)
  - `JobStep` entity (name, status, progress, retry policy, logs pointer)
  - `Connection` aggregate root (type, endpoint, encrypted secret ref, validated_at)
  - `Artifact` entity (format, checksum, size, storage URI)
  - `StorageTarget` value object (local / s3 / ceph-rbd / ceph-s3)
  - `License` aggregate root (plan, features, limits, expiry, signature, activation bindings)
  - Job state machine: `Created → Queued → Running → Pausing → Paused → Resuming → Cancelling → Cancelled → Failed → Succeeded`
  - Step state machine: `Pending → Running → Retrying → Failed → Skipped → Succeeded`
  - Domain events: `JobCreated`, `JobStatusChanged`, `StepCompleted`, `StepFailed`, `ArtifactUploaded`
  - Invariant enforcement in aggregate methods (not anemic)
  - `IMigrationStrategy` abstraction + `FullCopyStrategy` + `IncrementalStrategy`

- [x] **1-shared** — Shared Contracts *(depends on: 1-scaffold)*
  - `Result<T>` / `Result` types (success/failure with error codes)
  - Error code constants
  - `CorrelationId` value object + helper
  - Telemetry helper extensions

---

## Phase 2: Application + Infrastructure Layer

- [x] **2-app-ports** — Application Layer Ports *(depends on: 1-domain-core, 1-shared)*
  - `IJobRepository`, `IConnectionRepository`, `IArtifactRepository`
  - `IStorageAdapter` (upload, download, multipart, checksum, resume, metadata)
  - `IVSphereClient` (list VMs, get disk info, export/download VMDK, CBT query)
  - `IPveClient` (create VM, import disk, configure hardware)
  - `IEncryptionService` (encrypt/decrypt connection secrets — DataProtection wrapper)
  - `ILicenseService` (validate, activate, check features/limits)
  - `INotificationService` (SignalR push abstraction)

- [x] **2-app-usecases** — Application Layer Use Cases *(depends on: 2-app-ports)*
  - Commands: `CreateJob`, `CancelJob`, `PauseJob`, `ResumeJob`, `RetryFailedSteps`, `ResumeFromStep`
  - Commands: `CreateConnection`, `ValidateConnection`, `DeleteConnection`
  - Queries: `GetJob`, `ListJobs`, `GetJobProgress`, `GetConnections`, `GetArtifacts`
  - MassTransit saga: `MigrationJobSaga` (orchestrates step sequence, handles retries, cancellation)
  - Hangfire jobs: `CleanupExpiredArtifacts`, `SyncScheduledJobs`

- [x] **2-infra-db** — Infrastructure: Database + EF Core *(depends on: 2-app-ports)*
  - `AppDbContext` with entity configurations
  - Tables: `connections`, `jobs`, `job_steps`, `artifacts`, `storage_targets`, `licenses`, `audit_logs`
  - JSONB columns for `options` and `result` on `jobs`
  - Encrypted secret columns on `connections` (DataProtection)
  - Repository implementations (`JobRepository`, `ConnectionRepository`, etc.)

- [x] **2-infra-clients** — Infrastructure: External Clients *(depends on: 2-app-ports)*
  - `VSphereClient` — VMware SDK / REST calls (with mock mode)
  - `PveClient` — Proxmox VE API client (with mock mode)
  - `QemuImgWrapper` — shell-out to `qemu-img` with timeout, stderr capture, exit code, progress parsing
  - `StorageAdapterFactory` + implementations: `LocalStorageAdapter`, `S3StorageAdapter` (works for MinIO/Ceph-S3)
  - All external commands: `CancellationToken` support, configurable timeout

- [x] **2-infra-crypto** — Infrastructure: Security *(depends on: 2-app-ports)*
  - `DataProtectionEncryptionService` implementing `IEncryptionService`
  - Key management interface (`IKeyProvider`) — local file default, Vault/KMS pluggable
  - Audit log service (append-only writes)

- [x] **2-infra-telemetry** — Infrastructure: Observability *(depends on: 1-shared)*
  - OpenTelemetry configuration (tracing + metrics + logs export)
  - Serilog structured logging setup
  - Custom metrics: jobs_total, jobs_duration, step_duration, transfer_bytes
  - Health checks: PostgreSQL, Redis, RabbitMQ, MinIO

---

## Phase 3: API + Worker

- [x] **3-api** — ASP.NET Core Web API *(depends on: 2-app-usecases, 2-infra-db, 2-infra-telemetry)*
  - Minimal API endpoints for Jobs, Connections, Artifacts, License
  - SignalR hub for real-time job/step progress
  - Response compression (gzip/br)
  - Authentication + RBAC middleware (JWT-based, placeholder)
  - Global exception handling + ProblemDetails
  - Correlation ID middleware
  - OpenAPI/Swagger generation
  - `Program.cs` DI wiring (MassTransit, Hangfire, EF, Redis, Serilog, OTEL, SignalR)

- [x] **3-worker** — Background Worker Service *(depends on: 2-app-usecases, 2-infra-clients, 2-infra-crypto)*
  - MassTransit consumers for each step:
    - `ExportVmdkConsumer` (download from vSphere)
    - `ConvertDiskConsumer` (qemu-img convert vmdk → qcow2/raw)
    - `UploadArtifactConsumer` (upload to MinIO/S3/Ceph)
    - `ImportToPveConsumer` (import disk to Proxmox + configure VM)
    - `VerifyConsumer` (checksum verify + boot test placeholder)
  - Progress reporting via SignalR (through shared service or message bus)
  - CancellationToken propagation for every step
  - Retry policies per step (configurable)
  - Mock mode handlers (no real vSphere/PVE needed)

---

## Phase 4: Frontend

- [x] **4-frontend-scaffold** — Vue3 Project Setup *(depends on: 3-api)*
  - Vite + Vue3 + TypeScript
  - Vue Router, Pinia stores
  - API client (Axios wrapper with auto correlation ID headers)
  - SignalR client (`@microsoft/signalr`)

- [x] **4-frontend-pages** — Main Pages *(depends on: 4-frontend-scaffold)*
  - Dashboard (job list, status overview, summary cards)
  - New Migration wizard (source connection → target → storage → options → confirm)
  - Job detail page (step progress, logs, cancel/pause/retry)
  - Connections management (CRUD, validate)
  - Settings / License page

---

## Phase 5: Deployment

- [x] **5-compose** — Docker Compose *(depends on: 3-api, 3-worker)*
  - `docker-compose.yml`: API, Worker, PostgreSQL, Redis, RabbitMQ, MinIO
  - `docker-compose.build.yml`: multi-stage Dockerfiles for API, Worker, Frontend (nginx)
  - `.env.example` with all configurable values
  - `publish.sh` build + tag + push script

- [x] **5-helm** — Helm Chart *(depends on: 5-compose)*
  - Deployments: API, Worker, Frontend
  - StatefulSets/external: PostgreSQL, Redis, RabbitMQ, MinIO
  - `values-dev.yaml`, `values-prod.yaml`
  - Secrets management (sealed-secrets or external-secrets placeholder)
  - Ingress, HPA, PDB configs

---

## Phase 6: Documentation + License Server

- [x] **6-readme** — README（繁體中文） *(depends on: 3-api)*
  - 產品介紹、架構總覽
  - C4 Level 1–2 (ASCII)
  - 從 0 到跑起來的完整步驟
  - 選型說明 + ADR（MassTransit vs Hangfire, Storage 策略等）

- [x] **6-openapi** — OpenAPI Spec *(depends on: 3-api)*
  - YAML/JSON spec in `docs/`
  - All endpoints documented

- [x] **6-license-server** — License Server (Optional) *(depends on: 2-infra-crypto)*
  - `VMTO.LicenseServer` standalone service
  - License generation, validation, activation binding
  - Private key interface (file / Vault / KMS)
  - REST API for license operations

---

## Phase 7: Incremental Sync Architecture

- [x] **7-incremental** — Incremental Sync Roadmap *(depends on: 3-worker)*
  - CBT (Changed Block Tracking) integration design for vSphere
  - Alternative: file-level diff / qemu-img rebase
  - `IncrementalStrategy` implementation
  - Additional step consumers: `EnableCBT`, `IncrementalPull`, `ApplyDelta`, `FinalSyncCutover`
  - Documentation: architecture guide + phased rollout plan in `docs/`

---

## Phase 8: 版本管理、審查與安全掃描

- [x] **8-code-review** — 全專案程式碼審查
  - 各層 Code Review（Shared / Domain / Application / Infrastructure / API / Worker / Frontend / Infra / Helm）
  - 產出 `docs/code-review-report.md` 報告（含評分、問題清單、改進建議）

- [x] **8-security-scan** — 安全掃描
  - OWASP Top 10 覆蓋掃描
  - 產出 `docs/security-scan-report.md` 報告（含 24 項發現、修復優先級）

- [x] **8-versioning** — 集中式版本管理
  - `version.json` 作為 Single Source of Truth
  - `Directory.Build.props` — .NET 所有組件 VersionPrefix / AssemblyVersion / FileVersion
  - `frontend/package.json` — npm 版本
  - `helm/Chart.yaml` — appVersion
  - `ActivitySources.Version` — 從 Assembly 自動讀取
  - Container Image OCI Labels（`org.opencontainers.image.version`）
  - `docker-compose.build.yml` — Image tag 使用版本號
  - `publish.sh` — 自動讀取 version.json 並注入 build args
  - `infra/.env.example` — 新增 `VERSION` 變數
  - `.dockerignore` — 減小 build context

- [x] **8-security-hardening** — 安全強化
  - Dockerfile 全部改為非 root 使用者執行
  - nginx 加入安全回應標頭（X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy）
  - nginx `server_tokens off` 隱藏版本
  - nginx `client_max_body_size 10G` 支援大型 VMDK
  - SignalR proxy timeout 設為 3600s

- [x] **8-doc-update** — 文件更新
  - README 新增版本管理章節
  - README 新增報告文件索引
  - VMTO.API.http 更新為實際 endpoint
  - Tasks.md 新增 Phase 8
