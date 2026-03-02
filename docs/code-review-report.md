# VMTO 程式碼審查報告

> **審查日期：** 2026-02-24  
> **審查範圍：** 全專案（Shared / Domain / Application / Infrastructure / API / Worker / Frontend / Infra / Helm）  
> **整體評價：** ⭐⭐⭐⭐ (4/5) — 架構設計嚴謹、分層明確、DDD 實踐到位；仍有安全性與 pipeline 自動推進等重要改進空間。

---

## 1. 整體架構評估

| 面向 | 評分 | 說明 |
|------|------|------|
| **Clean Architecture 分層** | ⭐⭐⭐⭐⭐ | 依賴方向嚴格，Domain 層零框架依賴 |
| **DDD 實踐** | ⭐⭐⭐⭐ | 聚合根強制不變式、領域事件、值物件設計良好 |
| **CQRS 模式** | ⭐⭐⭐⭐⭐ | Command/Query 分離乾淨，介面設計清晰 |
| **Ports/Adapters** | ⭐⭐⭐⭐ | 介面在 Application 層、實作在 Infrastructure 層（`IAuditLogService` 例外） |
| **Event-Driven 架構** | ⭐⭐⭐⭐ | MassTransit Saga + Consumer 模式正確 |
| **前端架構** | ⭐⭐⭐⭐ | Vue3 Composition API + Pinia + SignalR 組合完整 |
| **部署架構** | ⭐⭐⭐⭐ | Docker Compose + Helm 涵蓋開發到生產 |

---

## 2. 各層審查結果

### 2.1 VMTO.Shared ⭐⭐⭐⭐⭐

- `Result<T>` / `Result` 型別設計穩健，`sealed class` + `private` 建構子
- `ErrorCodes` 巢狀靜態類別 + `const string` 前綴命名，便於全域唯一識別
- `CorrelationId` 使用 `readonly record struct`，零分配且值語意正確
- `MetricNames` 符合 OpenTelemetry 命名慣例（小寫、dot 分隔）
- **建議改進：** `Result<T>.Value` 加 `[MemberNotNullWhen]`、`CorrelationId.From()` 加驗證、`ActivitySource` 帶版本號

### 2.2 VMTO.Domain ⭐⭐⭐⭐

- 完全無框架依賴（✅ 最重要的規範）
- 聚合根（`MigrationJob`、`Connection`、`Artifact`、`License`）透過方法強制不變式，非貧血模型
- 狀態轉換使用 `Result` 回傳而非拋出例外
- Value Objects 使用 `sealed record`，天然不可變
- **建議改進：**
  - `JobStep.Retry()` 應重設 `Progress = 0`
  - `JobStep.LogsUri` 無任何 setter 方法
  - Value Objects（`Checksum`, `StorageTarget`）缺少建構時驗證
  - `EncryptedSecret` 應覆寫 `ToString()` 避免洩漏密文
  - Domain 事件 `StepCompletedEvent` / `StepFailedEvent` 未在聚合中 raise

### 2.3 VMTO.Application ⭐⭐⭐⭐⭐

- CQRS 分離清晰，Command / Query 使用 `sealed record`
- Port 介面全部定義在 Application 層，Repository 不回傳 `Result`（合理設計）
- DTO 正確隱藏敏感資訊（`ConnectionDto` 省略 Secret）
- `IStorageAdapter` / `IVSphereClient` / `IPveClient` 抽象完整
- **建議改進：** `ListJobsQuery.PageSize` / `Page` 無上下界驗證

### 2.4 VMTO.Infrastructure ⭐⭐⭐½

- EF Core Fluent API 配置完整，snake_case 命名
- S3 Multipart upload 含 abort 清理邏輯
- `QemuImgService` 有 timeout、stderr 捕獲、CancellationToken 傳播
- **問題：**
  - `IAuditLogService` 介面放在 Infrastructure 層（違反 Ports 模式）
  - `DependencyInjection.cs` 硬編碼預設連線字串含密碼
  - DataProtection 未配置 key 持久化（容器重啟後密鑰遺失）
  - `VSphereClient` / `PveClient` 完全不使用 `connectionId`
  - `LocalStorageAdapter` 有路徑穿越漏洞（詳見安全掃描報告）
  - `MockPveClient._nextVmId++` 非 thread-safe

### 2.5 VMTO.API ⭐⭐⭐½

- Minimal API + MapGroup + WithTags 模式清晰
- `GlobalExceptionHandler` 使用 `[LoggerMessage]` 高效能 source generator
- `CorrelationIdMiddleware` 正確使用 `OnStarting` 回呼
- **問題：**
  - 完全無 Authentication / Authorization
  - Endpoint 缺乏輸入驗證
  - Domain 方法回傳 `Result` 但 endpoint 未檢查
  - `pageSize` 無上限
  - API 同時執行 HangfireServer（水平擴展問題）
  - `VMTO.API.http` 殘留 scaffolding 模板

### 2.6 VMTO.Worker ⭐⭐⭐⭐

- 所有 Consumer 遵循一致模式：Load → Start → Work → Complete/Fail → Publish
- `[LoggerMessage]` source generator 用於所有日誌
- CancellationToken 正確傳播
- **問題：**
  - Saga InMemoryRepository（重啟遺失狀態）
  - Saga 不推進下一步（pipeline 無法自動銜接）
  - 4 個 incremental sync consumers 未在 Program.cs 註冊
  - Consumer 中 `async void` Progress 回呼
  - 大量重複的 `FailStepAsync` 程式碼

### 2.7 Frontend ⭐⭐⭐⭐

- Vue3 Composition API + `<script setup>` 模式
- Pinia stores + API client + SignalR composable 完整
- 型別定義與後端 DTO 完美對應
- **問題：** 缺少 ESLint 依賴、無 404 路由、delete 操作無確認、分頁邏輯不完整

### 2.8 Infra / Helm ⭐⭐⭐⭐

- Docker multi-stage build 正確
- Helm Chart 結構完整（dev/prod values + HPA + Ingress）
- **問題：** 容器以 root 執行、nginx 缺少安全標頭、缺少 `.dockerignore`、Helm deploy 缺少 `securityContext`

---

## 3. 程式碼品質統計

| 指標 | 數值 |
|------|------|
| .NET 專案數 | 7 (Shared, Domain, Application, Infrastructure, API, Worker, LicenseServer) |
| 測試專案數 | 4 |
| Frontend 元件 | 5 Views + 1 App + 5 Composables/Stores |
| Nullable 全啟用 | ✅ |
| TreatWarningsAsErrors | ✅ |
| file-scoped namespace | ✅ 全部統一 |
| sealed class 使用 | ✅ 所有非繼承類別 |
| CancellationToken 覆蓋率 | ✅ > 95% |

---

## 4. 建議優先處理項目

| 優先級 | 項目 | 影響 |
|--------|------|------|
| 🔴 P0 | 加入 Auth/AuthZ | 所有 API 暴露無保護 |
| 🔴 P0 | Saga 持久化 | Worker 重啟遺失狀態 |
| 🔴 P0 | Saga 推進機制 | Pipeline 無法自動銜接 |
| 🟡 P1 | DataProtection key 持久化 | 容器重啟密鑰遺失 |
| 🟡 P1 | Endpoint 輸入驗證 | 無效資料可能破壞系統 |
| 🟡 P1 | 容器非 root 執行 | 安全合規 |
| 🟢 P2 | Domain Result 檢查 | Endpoint 忽略錯誤回傳值 |
| 🟢 P2 | Consumer 冪等性 | 重複消費風險 |
| 🟢 P2 | nginx 安全標頭 | 防禦 XSS/Clickjacking |

---

## 5. 修復紀錄（2026-03-02）

| # | 問題 | 位置 | 修復方式 | 狀態 |
|---|------|------|----------|------|
| CR-01 | Saga 使用 InMemoryRepository，Worker 重啟後狀態遺失 | `src/VMTO.Worker/Program.cs` | 新增 `MigrationSagaDbContext`，改用 `EntityFrameworkSagaRepository`，Saga 狀態持久化至 PostgreSQL | ✅ 已修復 |
| CR-02 | Saga 在 StepCompleted 後不自動推進下一步，pipeline 無法銜接 | `src/VMTO.Worker/Sagas/MigrationJobSaga.cs` | 在各狀態的 `StepCompleted` 處理中加入 `.PublishAsync()`，自動發布下一步 Consumer message；同時更新 `JobStartedMessage` 攜帶 `StepIds` 與步驟參數，Consumers 於 `StepCompletedMessage.OutputData` 回傳執行結果 | ✅ 已修復 |
| CR-03 | 4 個 Incremental Sync Consumers 未在 Worker 啟動時註冊 | `src/VMTO.Worker/Program.cs` | 補上 `EnableCbtConsumer`、`IncrementalPullConsumer`、`ApplyDeltaConsumer`、`FinalSyncCutoverConsumer` 的 `AddConsumer<>()` 註冊 | ✅ 已修復 |
| CR-04 | `IAuditLogService` 介面定義在 Infrastructure 層，違反 Ports/Adapters | `src/VMTO.Infrastructure/Security/AuditLogService.cs` | 將介面移至 `src/VMTO.Application/Ports/Services/IAuditLogService.cs`，更新 Infrastructure 引用 | ✅ 已修復 |
| CR-05 | `MockPveClient._nextVmId++` 非 Thread-safe | `src/VMTO.Infrastructure/Clients/MockPveClient.cs` | 改用 `Interlocked.Increment(ref _nextVmId)` | ✅ 已修復 |
| CR-06 | `JobStep.Retry()` 未重設 Progress | `src/VMTO.Domain/Aggregates/MigrationJob/JobStep.cs` | 在 `Retry()` 中加入 `Progress = 0;` | ✅ 已修復 |
| CR-07 | `JobStep.LogsUri` 無 setter，無法記錄日誌 URI | `src/VMTO.Domain/Aggregates/MigrationJob/JobStep.cs` | 新增 `SetLogsUri(string uri)` 方法，限定 Running/Succeeded 狀態才允許設定 | ✅ 已修復 |
| CR-08 | `Checksum` 缺少建構時驗證 | `src/VMTO.Domain/ValueObjects/Checksum.cs` | 改為完整屬性定義，加入 `ArgumentException.ThrowIfNullOrEmpty` 驗證 | ✅ 已修復 |
| CR-09 | `StorageTarget` 缺少建構時驗證 | `src/VMTO.Domain/ValueObjects/StorageTarget.cs` | 改為完整屬性定義，加入 `ArgumentException.ThrowIfNullOrEmpty` 驗證 | ✅ 已修復 |
| CR-10 | `EncryptedSecret.ToString()` 可能洩漏密文 | `src/VMTO.Domain/ValueObjects/EncryptedSecret.cs` | 覆寫 `ToString()` 回傳 `"[REDACTED]"` | ✅ 已修復 |
| CR-11 | `StepCompletedEvent` / `StepFailedEvent` 未在 JobStep 中 raise | `src/VMTO.Domain/Aggregates/MigrationJob/JobStep.cs` | 為 `JobStep` 加入 `_domainEvents` 集合及 `ClearDomainEvents()`，在 `Complete()` 和 `Fail()` 中 raise 對應 Domain Event | ✅ 已修復 |
| CR-12 | `ListJobsQuery` 缺少 Page/PageSize 上下界驗證 | `src/VMTO.Application/Queries/Jobs/ListJobsQuery.cs` | 改為具驗證建構子，`Page >= 1`、`PageSize` 介於 1~100 | ✅ 已修復 |
| CR-13 | Consumer 中 `async void` Progress 回呼，例外不會被捕獲 | `src/VMTO.Worker/Consumers/ConvertDiskConsumer.cs`<br>`ExportVmdkConsumer.cs`<br>`ImportToPveConsumer.cs` | 改為同步 lambda（`_ = notifications.Send...` 非同步觸發），移除 `async void` | ✅ 已修復 |
| CR-14 | 多個 Consumer 大量重複的 `FailStepAsync` 程式碼 | `src/VMTO.Worker/Consumers/` | 提取至 `ConsumerHelper.FailStepAsync()` 共用靜態方法 | ✅ 已修復 |
| CR-15 | `Result<T>.Value` 缺少 `[MemberNotNullWhen]` | `src/VMTO.Shared/Result.cs` | 加入 `[MemberNotNullWhen(true, nameof(IsSuccess))]` | ✅ 已修復 |
| CR-16 | `CorrelationId.From()` 未驗證輸入 | `src/VMTO.Shared/CorrelationId.cs` | 加入 `ArgumentException.ThrowIfNullOrEmpty` 驗證 | ✅ 已修復 |
| CR-17 | Endpoint 未檢查 Domain `Result.IsSuccess` | `src/VMTO.API/Endpoints/JobEndpoints.cs` | `CancelJob`、`PauseJob`、`ResumeJob` 均加入 Result 檢查，失敗時回傳 `Results.BadRequest` | ✅ 已修復 |

### 評分更新（修復後）

| 層 | 原評分 | 修復後評分 | 改善說明 |
|----|--------|------------|----------|
| VMTO.Shared | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | `[MemberNotNullWhen]` + `CorrelationId.From()` 驗證 |
| VMTO.Domain | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Domain Events、Progress 重設、SetLogsUri、Value Object 驗證 |
| VMTO.Application | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | IAuditLogService 移入正確層、ListJobsQuery 驗證 |
| VMTO.Infrastructure | ⭐⭐⭐½ | ⭐⭐⭐⭐ | MockPveClient thread-safe、IAuditLogService 引用修正 |
| VMTO.API | ⭐⭐⭐½ | ⭐⭐⭐⭐ | Domain Result 檢查，BadRequest 回傳 |
| VMTO.Worker | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Saga 持久化、自動推進、Consumer 註冊完整、async void 修復、共用 Helper |
