# Tasks — VMTO (VM Transfer Orchestrator)

> Phase 1–8 已完成 ✅ | Phase 9–11 進化計畫 ⏳

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

- [x] **8-ci-workflows** — GitHub Actions CI 工作流程
  - `code-review.yml` — PR 程式碼審查（編譯、測試、格式、前端型別檢查）+ 自動修正 + 摘要報告
  - `codeql-security.yml` — CodeQL 安全掃描（C# + TypeScript/Vue）+ SARIF 解析 + 安全報告

---

## Phase 9: 短期補齊基礎 ⏳

- [ ] **S1** — global.json + 版本鎖定
  - 新增 `global.json` 鎖定 .NET 10.0.x SDK 版本
  - 確保所有開發者與 CI 使用一致的 SDK

- [ ] **S2** — appsettings.json 完善
  - API: 加入 ConnectionStrings（PostgreSQL, Redis, RabbitMQ, Hangfire）、Storage（S3）、MockMode、Cors:Origins、License:SigningKey
  - Worker: 加入 ConnectionStrings、Storage、MockMode
  - LicenseServer: 加入 ConnectionStrings、License:SigningKey
  - 各專案加入 `appsettings.Development.json` 使用 localhost 預設值

- [ ] **S3** — EF Core Initial Migration *(depends on: S2)*
  - 安裝 `dotnet-ef` tool
  - 執行 `dotnet ef migrations add InitialCreate --project src/VMTO.Infrastructure --startup-project src/VMTO.API`
  - 驗證 Migration 檔案正確產生所有表（jobs, job_steps, connections, artifacts, licenses, audit_logs）

- [ ] **S4** — Domain 層單元測試 *(depends on: S1)*
  - MigrationJob 狀態機測試：每個合法/非法轉換（10 個方法 × 有效/無效 ≈ 20+ 測試）
  - JobStep 狀態機測試：Start/Complete/Fail/Skip/Retry/UpdateProgress（≈ 15 測試）
  - Connection 測試：MarkValidated, UpdateSecret
  - License 測試：IsValid, HasFeature, IsExpired
  - 值物件測試：StorageTarget, Checksum, EncryptedSecret, MigrationOptions 的相等性
  - 策略測試：FullCopyStrategy.GetStepNames(), IncrementalStrategy.GetStepNames()
  - 領域事件：驗證 aggregate 方法正確 raise events

- [ ] **S5** — Application Handler 實作 + 測試 *(depends on: S4)*
  - 實作 8 個 CommandHandler：CreateJob, CancelJob, PauseJob, ResumeJob, RetryFailedSteps, CreateConnection, ValidateConnection, DeleteConnection
  - 實作 5 個 QueryHandler：GetJob, ListJobs, GetJobProgress, GetConnections, GetArtifacts
  - 在 Infrastructure 或 Application 層的 DI 註冊所有 handlers
  - 單元測試用 NSubstitute mock repositories 與 services

- [ ] **S6** — 文件更新 *(depends on: S5)*
  - 更新 `copilot-instructions.md` 加入 CI/CD 工作流程區段
  - 更新 `README.md` 加入版本管理與 CI 區段

---

## Phase 10: 中期強化品質與安全 ⏳

- [ ] **M1** — 整合測試（Testcontainers） *(depends on: S3, S5)*
  - 新增 `VMTO.IntegrationTests` 專案
  - 使用 Testcontainers 啟動 PostgreSQL、RabbitMQ、MinIO
  - 測試完整遷移流程（Mock 模式）：建立 Connection → 建立 Job → 執行 Steps → 驗證結果
  - 測試 Repository 層真實 DB 操作

- [ ] **M2** — JWT 認證實作 *(depends on: S5)*
  - 實作 JWT token 產生與驗證
  - 定義角色：Admin, Operator, Viewer
  - API endpoints 加入 `[Authorize]` 與角色檢查
  - 前端加入 login 頁面與 token 管理

- [ ] **M3** — API Rate Limiting *(depends on: M2)*
  - 使用 ASP.NET Core 內建 Rate Limiting middleware
  - 設定 fixed window / sliding window 策略
  - 依角色不同限制（Admin 較高）

- [ ] **M4** — 前端 E2E 測試（Playwright） *(depends on: M2)*
  - 安裝 Playwright
  - 測試場景：Dashboard 載入、建立連線、建立遷移任務、檢視進度
  - CI workflow 整合

- [ ] **M5** — 統一錯誤回應格式 *(depends on: S5)*
  - 建立 `ErrorResponse` DTO（code, message, details, correlationId）
  - API 所有 endpoint 回傳統一格式
  - ErrorCodes 對應 HTTP status code mapping
  - 前端 API client 統一錯誤處理

---

## Phase 11: 長期產品化進化 ⏳

- [ ] **L1** — Webhook / Event 通知 *(depends on: M2)*
  - 定義 `IWebhookService` 介面
  - 支援 Slack、Teams、Email、自訂 HTTP webhook
  - Job 完成/失敗時觸發
  - 設定頁面管理 webhook endpoints

- [ ] **L2** — Dashboard 圖表 *(depends on: M1)*
  - 整合 ECharts 或 Chart.js
  - 遷移統計（成功/失敗/進行中）
  - 趨勢圖（每日/每週遷移量）
  - 傳輸量統計

- [ ] **L3** — Native AOT 編譯 *(depends on: M1)*
  - 評估 .NET 10 AOT 相容性
  - 處理 reflection-heavy 套件（EF Core, MassTransit）的 AOT trimming
  - Dockerfile 加入 AOT build variant

- [ ] **L4** — i18n 國際化 *(depends on: M5)*
  - 前端整合 `vue-i18n`
  - 抽取所有 hardcoded 繁體中文字串
  - 支援 en-US, zh-TW, zh-CN
  - API 錯誤訊息多語系

- [ ] **L5** — Audit Dashboard *(depends on: M2)*
  - 新增 AuditLog 查詢 API（分頁、篩選）
  - 前端 Audit Log 頁面（表格、搜尋、匯出）
  - 時間軸視覺化
