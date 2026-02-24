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
