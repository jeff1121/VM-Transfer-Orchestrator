....
...
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

## Phase 9: 短期補齊基礎 ✅

- [x] **S1** — global.json + 版本鎖定
  - 新增 `global.json` 鎖定 .NET 10.0.x SDK 版本
  - 確保所有開發者與 CI 使用一致的 SDK

- [x] **S2** — appsettings.json 完善
  - API: 加入 ConnectionStrings（PostgreSQL, Redis, RabbitMQ, Hangfire）、Storage（S3）、MockMode、Cors:Origins、License:SigningKey
  - Worker: 加入 ConnectionStrings、Storage、MockMode
  - LicenseServer: 加入 ConnectionStrings、License:SigningKey
  - 各專案加入 `appsettings.Development.json` 使用 localhost 預設值

- [x] **S3** — EF Core Initial Migration *(depends on: S2)*
  - 安裝 `dotnet-ef` tool
  - 執行 `dotnet ef migrations add InitialCreate --project src/VMTO.Infrastructure --startup-project src/VMTO.API`
  - 驗證 Migration 檔案正確產生所有表（jobs, job_steps, connections, artifacts, licenses, audit_logs）

- [x] **S4** — Domain 層單元測試（130 個） *(depends on: S1)*
  - MigrationJob 狀態機測試：每個合法/非法轉換（10 個方法 × 有效/無效 ≈ 20+ 測試）
  - JobStep 狀態機測試：Start/Complete/Fail/Skip/Retry/UpdateProgress（≈ 15 測試）
  - Connection 測試：MarkValidated, UpdateSecret
  - License 測試：IsValid, HasFeature, IsExpired
  - 值物件測試：StorageTarget, Checksum, EncryptedSecret, MigrationOptions 的相等性
  - 策略測試：FullCopyStrategy.GetStepNames(), IncrementalStrategy.GetStepNames()
  - 領域事件：驗證 aggregate 方法正確 raise events

- [x] **S5** — Application Handler 實作 + 測試（29 個） *(depends on: S4)*
  - 實作 8 個 CommandHandler：CreateJob, CancelJob, PauseJob, ResumeJob, RetryFailedSteps, CreateConnection, ValidateConnection, DeleteConnection
  - 實作 5 個 QueryHandler：GetJob, ListJobs, GetJobProgress, GetConnections, GetArtifacts
  - Application DI 註冊所有 13 個 handlers
  - 單元測試用 NSubstitute mock repositories 與 services

- [x] **S6** — 文件更新 *(depends on: S5)*
  - 更新 `copilot-instructions.md` 加入 CI/CD 工作流程區段與 Database 區段
  - Tasks.md 更新至 Phase 9-11

---

## Phase 10: 中期強化品質與安全 ✅

- [x] **M1** — 整合測試（Testcontainers） *(depends on: S3, S5)*
  - 新增 `VMTO.IntegrationTests` 專案
  - 使用 Testcontainers 啟動 PostgreSQL 容器 + 自動 EF Core 遷移
  - JobRepositoryTests: 5 個測試（新增、含步驟讀取、狀態更新、分頁篩選、計數）
  - ConnectionRepositoryTests: 3 個測試（新增、刪除、驗證時間持久化）

- [x] **M2** — JWT 認證實作 *(depends on: S5)*
  - 實作 JwtSettings / Roles / JwtTokenService 認證基礎建設
  - 新增 POST /api/auth/login 登入端點
  - API 所有端點加入 RequireAuthorization（寫入操作限 Admin/Operator）
  - Swagger 加入 Bearer token 安全定義
  - SignalR 支援 query string JWT token
  - 前端: LoginView、auth Pinia store、Router 守衛、API client 自動附加 token

- [x] **M3** — API Rate Limiting *(depends on: M2)*
  - 全域速率限制：依角色分級（Admin 300/min、Operator 120/min、Viewer 60/min）
  - 寫入操作策略：每分鐘 30 次上限
  - 登入端點策略：每分鐘 10 次防暴力破解
  - 429 回應使用統一 ErrorResponse 格式

- [x] **M4** — 前端 E2E 測試（Playwright） *(depends on: M2)*
  - 安裝 @playwright/test + 建立 playwright.config.ts
  - 3 個測試檔案：login、dashboard、connections
  - CI workflow: .github/workflows/e2e-tests.yml

- [x] **M5** — 統一錯誤回應格式 *(depends on: S5)*
  - 建立 `ErrorResponse` DTO（code, message, details, correlationId）
  - 建立 `ErrorCodeMapping`（ErrorCode → HTTP 狀態碼映射表）
  - 建立 `ResultExtensions`（Result/Result<T> → IResult 轉換）
  - GlobalExceptionHandler 改用 ErrorResponse 格式
  - 前端 API client 401 自動導向登入

---

## Phase 11: 長期產品化進化 ✅

- [x] **L1** — Webhook / Event 通知 *(depends on: M2)* ✅
  - ✅ 定義 `IWebhookService` 介面 + `WebhookService` 實作（Http/Slack/Teams/Email）
  - ✅ `WebhookSubscription` 實體 + EF Core 設定
  - ✅ Webhook CRUD API（`/api/webhooks`，Admin 專用）+ HMAC-SHA256 簽章
  - ✅ 前端 `WebhooksView.vue` 管理頁面

- [x] **L2** — Dashboard 圖表 *(depends on: M1)* ✅
  - ✅ 整合 ECharts + vue-echarts 圖表庫
  - ✅ `DashboardEndpoints`（`/api/dashboard/stats`）：狀態統計、每日趨勢、平均耗時
  - ✅ 狀態分布圓餅圖 + 每日遷移趨勢折線圖
  - ✅ 統計摘要卡片（總任務數、平均耗時、總傳輸量）

- [x] **L3** — Native AOT 編譯 *(depends on: M1)* ✅
  - ✅ ADR-005 評估文件：各元件 AOT 相容性分析
  - ✅ Shared/Domain 層加入 `IsTrimmable` + `IsAotCompatible` 標記
  - ✅ 結論：EF Core/MassTransit 暫不支援 AOT，保持 JIT 模式

- [x] **L4** — i18n 國際化 *(depends on: M5)* ✅
  - ✅ 整合 `vue-i18n`，支援 zh-TW（預設）、en-US、zh-CN
  - ✅ 三個完整語系檔 + 所有 View 元件抽取字串
  - ✅ SettingsView 語言切換下拉 + localStorage 持久化
  - ✅ ECharts 圖表標題/圖例即時語言切換

- [x] **L5** — Audit Dashboard *(depends on: M2)* ✅
  - ✅ `AuditEndpoints`（`/api/audit`）：分頁查詢、CSV 匯出（UTF-8 BOM）、統計摘要
  - ✅ 前端 `AuditView.vue`：篩選列、表格、分頁、時間軸視覺化
  - ✅ Pinia audit store + API client

---

## Phase 12: 進階進化 — E1 → E3 → E4/E5 ⏳

> **實作順序**：E1（可觀測性）→ E3（容錯韌性）→ E4（前端體驗）/ E5（營運自動化）可平行

---

### E1 — 可觀測性與監控體系（Observability Stack）

> **目標**：建立完整分散式追蹤、指標收集、日誌聚合體系

- [x] **E1.1** — OpenTelemetry 基礎建設
  - 安裝 `OpenTelemetry.Extensions.Hosting`、`OpenTelemetry.Instrumentation.AspNetCore`、`OpenTelemetry.Instrumentation.Http`、`OpenTelemetry.Instrumentation.EntityFrameworkCore`
  - 在 `Infrastructure/Telemetry/` 擴展 `AddVmtoTelemetry()` 加入 TracerProvider + MeterProvider
  - 設定 OTLP exporter（支援 Jaeger / Grafana Tempo）
  - API 與 Worker 專案啟用自動儀器化

- [x] **E1.2** — 自訂追蹤 Span
  - 每個 Worker Consumer 加入自訂 Activity/Span（步驟名稱、JobId、StepId）
  - qemu-img 轉換過程加入 Span（含進度百分比 tag）
  - S3 上傳加入 Span（含 chunk 編號、大小）
  - vSphere/PVE client 呼叫加入 Span

- [x] **E1.3** — Prometheus 指標暴露
  - 安裝 `OpenTelemetry.Exporter.Prometheus.AspNetCore`
  - 建立 `VmtoMetrics` 靜態類定義所有 Counter/Histogram/Gauge：
    - `vmto_jobs_total{status, strategy}` — 任務計數器
    - `vmto_step_duration_seconds{step_name, status}` — 步驟耗時直方圖
    - `vmto_transfer_bytes_total` — 傳輸位元組數
    - `vmto_active_jobs` — 當前執行中任務數 Gauge
    - `vmto_queue_depth{queue_name}` — Queue 深度
  - 暴露 `/metrics` endpoint

- [x] **E1.4** — 結構化日誌強化
  - 安裝 `Serilog.Enrichers.CorrelationId`
  - 加入自訂 Enricher：JobId、StepId（從 AsyncLocal 讀取）
  - 設定 Serilog Sink 輸出至 Console（JSON 格式）+ File（滾動）
  - 敏感資料自動遮罩（password、secret、token 欄位）

- [x] **E1.5** — 進階 Health Check
  - `DbMigrationHealthCheck` — 驗證 DB migration 版本是否最新
  - `RabbitMqHealthCheck` — 驗證 RabbitMQ 連線 + queue 存在
  - `MinioHealthCheck` — 驗證 MinIO bucket 存在 + 可寫入
  - `DiskSpaceHealthCheck` — 驗證磁碟可用空間 > 閾值
  - `/health/ready` vs `/health/live` 分離
  - Health Check UI 頁面（`AspNetCore.HealthChecks.UI`）

- [x] **E1.6** — Grafana Dashboard + Docker Compose 整合
  - `infra/docker-compose.yml` 新增 Jaeger + Prometheus + Grafana 容器
  - 提供預建 Grafana JSON Dashboard（遷移概覽、效能分析、錯誤追蹤）
  - Prometheus `prometheus.yml` 自動 scrape API + Worker
  - 更新 `.env.example` 加入 OTEL 設定

---

### E3 — 容錯與韌性工程（Resilience Engineering）*(depends on: E1)*

> **目標**：確保系統在外部服務故障時優雅降級並自動恢復

- [x] **E3.1** — Circuit Breaker 模式（Polly v8）
  - 安裝 `Microsoft.Extensions.Http.Resilience`
  - `vSphere API`：5 次失敗開啟斷路器，30 秒後半開
  - `PVE API`：同上策略
  - `S3/MinIO`：3 次失敗開啟，60 秒後半開
  - 斷路器狀態變更時記錄日誌 + 觸發 Webhook 通知
  - 斷路器狀態暴露為 Prometheus 指標（`vmto_circuit_breaker_state`）

- [x] **E3.2** — 統一 Retry 策略
  - 定義 `RetryPolicyOptions` 設定類（可從 appsettings 讀取）
  - 指數退避 + 抖動（jitter）：base 1s, max 30s, factor 2
  - 分類可重試錯誤（timeout, 5xx, connection reset）vs 不可重試（4xx, disk full）
  - Worker Consumer 的 MassTransit retry 設定統一管理
  - 每次 retry 記錄日誌（含 attempt 次數、等待時間）

- [x] **E3.3** — Dead-Letter Queue（DLQ）
  - MassTransit `_error` / `_skipped` queue 設定
  - `DlqConsumer`：消費失敗訊息 → 記錄至 `dead_letter_logs` 表 → 通知 Admin
  - 新增 `DeadLetterLogEntry` EF 實體 + Configuration
  - API endpoint `POST /api/ops/dlq/{id}/replay` — 重發單筆失敗訊息
  - API endpoint `GET /api/ops/dlq` — 列出 DLQ 訊息（分頁）

- [x] **E3.4** — 超時與取消強化
  - 所有 `HttpClient` 呼叫套用 Polly timeout policy（outer timeout）
  - Worker Consumer heartbeat 機制（定期報告進度防止 RabbitMQ prefetch timeout）
  - `CancellationToken` 全鏈路傳播驗證（撰寫測試確認）
  - qemu-img 執行加入 process-level timeout（kill -9 fallback）

- [x] **E3.5** — Graceful Shutdown
  - Worker 收到 `SIGTERM` → 停止接受新訊息 → 等待 in-flight 完成（最長 60 秒）
  - 實作 `IHostedService.StopAsync()` 正確處理
  - Docker Compose `stop_grace_period: 90s` 設定
  - Helm `terminationGracePeriodSeconds: 90` 設定
  - Shutdown 過程記錄日誌

- [x] **E3.6** — Chaos Testing 就緒
  - 建立 `IChaosPolicy` 介面 + `ChaosDecorator<T>` 泛型裝飾器
  - 支援注入：隨機延遲（0-5s）、隨機失敗（可設定比率）、隨機 timeout
  - 設定驅動：`Chaos:Enabled`、`Chaos:FailureRate`、`Chaos:MaxDelayMs`
  - 僅在 Development/Staging 環境啟用
  - 可透過 API endpoint 動態開關（`POST /api/ops/chaos`）

---

### E4 — 前端體驗升級（Frontend DX & UX）*(可與 E5 平行)*

> **目標**：提升前端至產品級水準

- [x] **E4.1** — SignalR 增強
  - 自動重連 + UI 斷線提示橫幅（含倒計時秒數）
  - 連線品質指示器（ping 延遲 ms 顯示）
  - 重連失敗 N 次後提示「手動重新整理」
  - 連線狀態寫入 Pinia store，全域可用

- [x] **E4.2** — 深色模式
  - CSS custom properties 定義 light/dark 兩組色彩變數
  - 自動偵測 `prefers-color-scheme` 系統偏好
  - SettingsView 新增主題切換（自動/淺色/深色）
  - localStorage 持久化主題偏好
  - ECharts 圖表配色同步切換（dark theme）
  - 所有 View 元件適配深色模式

- [x] **E4.3** — PWA 支援
  - 安裝 `vite-plugin-pwa`
  - Service Worker：靜態資源 precache + runtime cache（API 回應）
  - Web App Manifest：圖示、名稱、啟動畫面
  - 離線回退頁面（顯示快取資料 + 離線提示）
  - 可安裝至桌面/手機主畫面

- [x] **E4.4** — 行動裝置適配
  - 響應式側邊欄：< 768px 改為 hamburger menu + 滑出抽屜
  - 表格元件：小螢幕自動切換為卡片佈局
  - 觸控友善：按鈕最小 44×44px、適當間距
  - 底部導航列（手機版）

- [x] **E4.5** — 通知中心
  - 右上角通知鈴鐺 icon（未讀計數 badge）
  - 即時 toast notification（成功/失敗/警告，3 秒自動消失）
  - 通知歷史抽屜（右側滑出面板）
  - 通知類型：任務完成、任務失敗、步驟失敗、系統公告
  - 已讀/未讀標記 + 全部標記已讀

---

### E5 — 營運自動化與自癒能力（Ops Automation）*(depends on: E1, E3；可與 E4 平行)*

> **目標**：減少人工介入，讓系統自動診斷、修復、擴縮容

- [x] **E5.1** — 智慧自動重試與自癒
  - 定義 `IErrorClassifier`：分析錯誤類型（暫時性 vs 永久性）
  - 暫時性錯誤（timeout, connection reset, 503）→ 自動延遲重試
  - 永久性錯誤（disk full, permission denied, 400）→ 標記失敗 + 通知
  - 卡住任務偵測：Running 超過 N 分鐘無進度更新 → 自動 cancel + 重新排隊
  - Hangfire recurring job：每 5 分鐘掃描卡住任務

- [x] **E5.2** — Hangfire 排程任務增強
  - `ArtifactCleanupJob`：清理過期 artifact（可設定保留天數）
  - `DailyReportJob`：每日摘要報告 → 透過 Webhook 發送
  - `StorageUsageJob`：每小時檢查 MinIO bucket 使用量，超過閾值告警
  - `HealthReportJob`：每 30 分鐘產生系統健康快照

- [x] **E5.3** — Ops API 端點
  - `GET /api/ops/health-report` — 系統健康報告
  - `GET /api/ops/stuck-jobs` — 列出卡住任務 + `POST .../heal` 一鍵修復
  - `GET /api/ops/storage-usage` — 儲存空間使用報告
  - `GET /api/ops/system-info` — 系統版本、runtime、uptime
  - 所有 Ops 端點限 Admin 角色

- [x] **E5.4** — KEDA 自動擴縮
  - Helm 新增 KEDA `ScaledObject` 定義
  - 基於 RabbitMQ queue 深度擴縮 Worker（min 1 / max 10）
  - HPA 基於 CPU/Memory 擴縮 API
  - 提供 `values-keda.yaml` 範例設定

- [x] **E5.5** — 備份與災難恢復
  - `POST /api/ops/backup/config` — 匯出系統設定為 JSON
  - `POST /api/ops/restore/config` — 匯入設定
  - `DatabaseBackupJob`（Hangfire）：每日 pg_dump → MinIO `backups` bucket
  - 災難恢復文件（`docs/disaster-recovery.md`）

---

## Phase 13: Multi-Platform Migration Foundation — Hyper-V → Proxmox VE ⏳

> **Epic**：讓 VMTO 從固定的 vSphere → Proxmox VE 流程，演進為可擴充來源與目標平台的遷移引擎。第一個正式擴充路徑為 **Hyper-V → Proxmox VE**。
>
> **Agile Board 使用方式**：每個 `13-F*` 建立為 Feature，每個 `13-US*` 建立為 User Story，縮排項目建立為 Task。Tag：`platform-extensibility`、`hyper-v`、`proxmox`、`architecture`。
>
> **MVP 限制**：僅支援已關機、無 checkpoint chain 的 Hyper-V VM；不刪除或修改來源 VM；不包含 live migration 與增量同步。

### 13-F1 — 平台抽象與相容性模型 *(Sprint 0)*

- [ ] **13-US1.1** — 定義來源與目標平台的獨立 Port
  - [ ] 建立 `IVmSourceAdapter`：validate、list、inspect、prepare/export、cleanup
  - [ ] 建立 `IVmTargetAdapter`：validate、capacity、provision、attach、configure、verify、rollback
  - [ ] 定義 `PlatformKind`，初始值為 `VSphere`、`HyperV`、`ProxmoxVE`
  - [ ] 建立 `IPlatformAdapterRegistry`，依平台解析 Adapter 並於啟動時驗證註冊完整性
  - [ ] 將既有 `IVSphereClient`、`IPveClient` 以相容 Adapter 包裝，避免一次性刪除既有實作

- [ ] **13-US1.2** — 建立平台中立的 VM 與能力資料模型
  - [ ] 建立 `PlatformCapabilities`：支援磁碟格式、韌體、Secure Boot、online export、incremental export、NIC model
  - [ ] 建立 `VmHardwareSpec`、`DiskDescriptor`、`NetworkSpec`、`ExportManifest`
  - [ ] 擴充 Connection 的 metadata schema，保存平台特有設定而不把秘密資訊寫入 metadata
  - [ ] 定義 API DTO 與 OpenAPI schema
  - [ ] 撰寫能力序列化、驗證與 backward-compatible migration tests

- [ ] **13-US1.3** — 記錄架構決策與相容性規則
  - [ ] 新增 ADR：來源／目標 Adapter 分離與 Hyper-V offline-only MVP
  - [ ] 建立來源／目標平台相容性矩陣
  - [ ] 定義 BIOS/UEFI、Secure Boot、disk bus、NIC model 的 mapping policy
  - [ ] 定義 unsupported feature 的標準錯誤碼與使用者訊息

### 13-F2 — 通用 Migration Plan 與 Saga *(Sprint 1)*

- [ ] **13-US2.1** — 將固定步驟改為 typed Migration Plan
  - [ ] 建立 `MigrationStepKind`：ExportDisk、ConvertDisk、StageArtifact、ProvisionTargetVm、AttachDisk、ConfigureTargetVm、VerifyTargetVm、Cleanup
  - [ ] 建立 `MigrationPlan`、version、adapter identity、step input/output contract
  - [ ] 實作 `MigrationPlanBuilder`，以來源／目標 capabilities 產生合法計畫
  - [ ] 在建立 Job 時持久化 plan snapshot，確保重試時不受後續設定變更影響
  - [ ] 為 plan validation 撰寫單元測試與拒絕不相容配對的測試

- [ ] **13-US2.2** — 重構 MassTransit Saga 與訊息
  - [ ] 將 `ExportVmdkMessage` 改為平台中立的 export message
  - [ ] 將 `ImportToPveMessage` 改為 provision／attach target messages
  - [ ] Saga 由 plan 推進下一步，不再依 consumer 名稱決定流程
  - [ ] Step output 改用結構化 payload，移除 `.vmdk`、`PveVmId` 等平台硬編碼 fallback
  - [ ] 補上 vSphere → Proxmox regression、pause/resume、retry/cancel test matrix

- [ ] **13-US2.3** — 建立補償與冪等性機制
  - [ ] 為每個可變更目標資源建立 idempotency key
  - [ ] 實作 target rollback port 與 partial VM cleanup
  - [ ] 實作 source export cleanup 與暫存 artifact retention policy
  - [ ] 建立 failure injection tests：轉檔、上傳、建 VM、attach disk、verify

### 13-F3 — Hyper-V Source Adapter *(Sprint 2)*

- [ ] **13-US3.1** — 建立安全的 Windows Hyper-V source agent
  - [ ] 決定並記錄 agent transport（優先：mTLS HTTPS agent；備選：constrained WinRM/PowerShell）
  - [ ] 建立 agent authentication、least-privilege service account 與 secret rotation configuration
  - [ ] 實作 health endpoint 與 host capability discovery
  - [ ] 建立 audit log，記錄 export request、操作者、VM identity、結果，且不得記錄 secret
  - [ ] 建立 Docker/Linux Worker 與 Windows source agent 的 integration test harness

- [ ] **13-US3.2** — 實作 Hyper-V discovery 與 pre-flight
  - [ ] 列出 Hyper-V VM、state、generation、firmware、CPU、memory、磁碟與 NIC
  - [ ] 取得 VHD/VHDX chain 與可用空間資訊
  - [ ] 拒絕 running VM、checkpoint/AVHDX chain、pass-through disk、unsupported dynamic disk
  - [ ] 產生含修復建議的 pre-flight report
  - [ ] 為 discovery response 與失敗情境建立 contract tests

- [ ] **13-US3.3** — 實作 Hyper-V offline export
  - [ ] 執行並監控 `Export-VM`，支援 cancellation、timeout、progress 與 structured errors
  - [ ] 收集 export manifest、VHD/VHDX path、configuration metadata、checksums
  - [ ] 透過既有 Storage Adapter 以 resumable multipart upload 上傳 artifact
  - [ ] 在中斷、取消與成功後執行來源暫存清理
  - [ ] 建立 mock agent 與至少一個實體 Windows Hyper-V integration scenario

### 13-F4 — Hyper-V → Proxmox VE 遷移 MVP *(Sprint 3)*

- [ ] **13-US4.1** — 支援 VHD/VHDX conversion
  - [ ] 擴充 `QemuImgService`，以安全 format detection 處理 VHD 與 VHDX
  - [ ] 支援 VHD/VHDX → QCOW2 與 VHD/VHDX → RAW 的 conversion profile
  - [ ] 在 conversion 前執行 image info/check，保存來源格式、virtual size、actual size 與 checksum
  - [ ] 驗證 backing chain 不存在或已合併；失敗時提供明確錯誤
  - [ ] 加入 VHD/VHDX fixture、cancellation、disk-full、corrupt-image tests

- [ ] **13-US4.2** — 將 Proxmox 實作移至通用 target adapter
  - [ ] 實作 `ProxmoxTargetAdapter`，封裝 create VM、import/attach disk、hardware/network configure、verify、rollback
  - [ ] 映射 Hyper-V Generation 1/2 至 PVE BIOS/OVMF 與 Secure Boot 設定
  - [ ] 映射 CPU、memory、disk bus、NIC model 與 network bridge
  - [ ] 實作 partial PVE VM cleanup 與 retry-safe resource lookup
  - [ ] 為既有 vSphere → PVE 及 Hyper-V → PVE 路徑共用 target contract tests

- [ ] **13-US4.3** — 完成 MVP 端對端驗證
  - [ ] 建立 Linux Generation 1、Linux Generation 2、Windows Generation 2 測試 VM 矩陣
  - [ ] 驗證 disk checksum、boot、guest IP/heartbeat、CPU/memory、NIC connectivity
  - [ ] 建立 dry-run、happy path、cancel、retry、rollback 的 e2e runbook
  - [ ] 將測試證據與限制更新至 `docs/`

### 13-F5 — API、前端與維運體驗 *(Sprint 4)*

- [ ] **13-US5.1** — 讓使用者可選擇平台與檢視相容性
  - [ ] Connection UI/API 支援 `HyperV` 與平台特有設定欄位
  - [ ] Create Job flow 顯示 source/target capability matrix 與不相容警告
  - [ ] 新增 pre-flight endpoint 與 UI 結果頁
  - [ ] 提供 dry-run 模式，不建立目標 VM 也不上傳完整磁碟
  - [ ] 新增 i18n（zh-TW/en-US/zh-CN）字串與 accessibility tests

- [ ] **13-US5.2** — 強化可觀測性與營運文件
  - [ ] 新增 source_platform、target_platform、adapter、plan_version 維度的 metrics/traces
  - [ ] 新增 export duration、conversion ratio、rollback total、unsupported capability metrics
  - [ ] 擴充 audit log 與 Ops health report 的平台資訊
  - [ ] 撰寫 Hyper-V agent deployment、least-privilege、troubleshooting、disaster recovery 文件
  - [ ] 建立 release checklist 與 staged pilot checklist

### 13-F6 — Agile Board 驗證 *(跨 Sprint)*

- [ ] **13-US6.1** — 將本 Phase 建立於 Azure Boards
  - [ ] 建立 Epic：`Multi-Platform Migration Foundation`
  - [ ] 建立 Features：`13-F1` 至 `13-F6`
  - [ ] 建立本文件所有 User Stories 與 Tasks，保留相同識別碼於標題開頭
  - [ ] 將 Sprint 0–4 建立為 Iteration，設定 Area Paths：Architecture、HyperV、Proxmox、UX、Quality
  - [ ] 建立 Dashboard：Feature progress、blocked work、platform test matrix、PR linkage
  - [ ] 驗證 branch、commit、PR description 使用 `AB#<work-item-id>` 後會正確回鏈
