# VMTO 安全掃描報告

> **掃描日期：** 2026-02-24  
> **掃描範圍：** 全專案原始碼（Backend / Frontend / Infrastructure / Helm）  
> **OWASP Top 10 覆蓋範圍：** A01–A10  
> **風險等級：** 🔴 高 × 6 | 🟡 中 × 10 | 🟢 低 × 8

---

## 1. 漏洞總覽

### 🔴 嚴重 / 高風險

| # | OWASP | 類別 | 位置 | 描述 |
|---|-------|------|------|------|
| S01 | A01 | **Broken Access Control** | `src/VMTO.API/Program.cs` + 所有 Endpoints | **完全無 Authentication / Authorization**。所有 REST API endpoint 開放存取，任何人可建立/取消任務、管理連線（含密碼）、啟動授權金鑰。 |
| S02 | A01 | **Broken Access Control** | `src/VMTO.Infrastructure/Hubs/MigrationHub.cs` | SignalR Hub 無 `[Authorize]`，任何人可監聽所有任務進度。 |
| S03 | A01 | **Broken Access Control** | `src/VMTO.API/Program.cs:65` | Hangfire Dashboard 未設定 `IDashboardAuthorizationFilter`，暴露完整排程管理介面。 |
| S04 | A03 | **Injection — Path Traversal** | `src/VMTO.Infrastructure/Storage/LocalStorageAdapter.cs:GetFullPath()` | `Path.Combine(_basePath, key)` 未驗證結果路徑。攻擊者傳入 `../../etc/passwd` 可讀寫任意檔案。 |
| S05 | A03 | **Injection — Command Injection** | `src/VMTO.Infrastructure/Clients/QemuImgService.cs:ConvertAsync()` | `inputPath` / `outputPath` 以 string interpolation 拼接到 `ProcessStartInfo.Arguments`。含 `"` 的檔名可逃脫引號。雖 `UseShellExecute=false` 降低風險，但仍非安全做法。 |
| S06 | A07 | **Identification Failures** | 全域 | 無任何身份驗證機制（JWT、API Key、Cookie 等），無法識別操作者身份。Audit log 的 `userId` 永遠為 `null`。 |

### 🟡 中風險

| # | OWASP | 類別 | 位置 | 描述 |
|---|-------|------|------|------|
| S07 | A02 | **Cryptographic Failures** | `src/VMTO.Infrastructure/DependencyInjection.cs:23` | `AddDataProtection()` 未配置 key 持久化目的地。容器重啟後金鑰遺失，所有已加密的 Connection Secret 無法解密。 |
| S08 | A02 | **Cryptographic Failures** | `src/VMTO.Infrastructure/DependencyInjection.cs:37` | S3 AccessKey/SecretKey 從 `IConfiguration` 明文讀取，缺失時以 `string.Empty` 替代而非拋錯。 |
| S09 | A05 | **Security Misconfiguration** | `src/VMTO.Infrastructure/DependencyInjection.cs:18` | 硬編碼預設連線字串 `"Host=localhost;Database=vmto;Username=vmto;Password=vmto"` 含密碼。 |
| S10 | A05 | **Security Misconfiguration** | `infra/nginx.conf` | 缺少所有安全回應標頭：`X-Content-Type-Options`、`X-Frame-Options`、`Content-Security-Policy`、`Referrer-Policy`、`Strict-Transport-Security`。nginx 版本資訊暴露（缺 `server_tokens off`）。 |
| S11 | A05 | **Security Misconfiguration** | `infra/dockerfiles/Dockerfile.*` | 所有容器以 root 使用者執行（API、Worker、Frontend nginx）。 |
| S12 | A05 | **Security Misconfiguration** | `helm/templates/*-deployment.yaml` | 缺少 Pod `securityContext`（`runAsNonRoot`、`readOnlyRootFilesystem`、`allowPrivilegeEscalation: false`）。 |
| S13 | A05 | **Security Misconfiguration** | `infra/docker-compose.yml` | Redis 無密碼保護；RabbitMQ/PostgreSQL/MinIO 密碼硬編碼預設值。 |
| S14 | A08 | **Data Integrity Failures** | `src/VMTO.Worker/Program.cs:14` | Saga 使用 `InMemoryRepository()`，Worker 重啟後所有進行中的遷移 saga 狀態遺失。 |
| S15 | A09 | **Logging Failures** | `src/VMTO.API/Middleware/CorrelationIdMiddleware.cs` | Client 傳入的 `X-Correlation-Id` 未驗證格式/長度，可注入任意字串至日誌（log injection 風險）。 |
| S16 | A10 | **SSRF** | `src/VMTO.Infrastructure/Clients/VSphereClient.cs` | `vmId`、`diskKey` 直接插入 URL path 未經驗證/編碼。`connectionId` 完全未使用，所有請求打到同一 BaseAddress。 |

### 🟢 低風險

| # | OWASP | 類別 | 位置 | 描述 |
|---|-------|------|------|------|
| S17 | A04 | **Insecure Design** | `src/VMTO.API/Endpoints/JobEndpoints.cs` | `pageSize` 無上限，可傳 `999999` 造成記憶體耗盡（DoS）。 |
| S18 | A04 | **Insecure Design** | `src/VMTO.API/Endpoints/ConnectionEndpoints.cs` | `DeleteConnection` 未檢查是否有 active job 正在使用該連線。 |
| S19 | A04 | **Insecure Design** | `src/VMTO.Infrastructure/Notifications/SignalRNotificationService.cs` | 使用 `Clients.All` 廣播 — 所有連線用戶可看到所有任務進度（資訊洩漏）。 |
| S20 | A04 | **Insecure Design** | `src/VMTO.API/Endpoints/JobEndpoints.cs:RetryFailedSteps()` | 直接操作 `JobStep.Retry()` 繞過 aggregate root，違反 DDD invariant 保護。 |
| S21 | A05 | **Security Misconfiguration** | `src/VMTO.API/Program.cs:41` | CORS 政策使用 `AllowAnyHeader` + `AllowAnyMethod` + `AllowCredentials`，過度寬鬆。 |
| S22 | A05 | **Security Misconfiguration** | 缺少 `.dockerignore` | Docker build context 包含 `.git`、`node_modules` 等不必要檔案，可能洩漏敏感資訊。 |
| S23 | A06 | **Vulnerable Components** | `src/VMTO.Infrastructure/VMTO.Infrastructure.csproj` | `OpenTelemetry.Exporter.Prometheus.AspNetCore` 使用 `1.15.0-beta.1` 預覽版。 |
| S24 | A04 | **Insecure Design** | `src/VMTO.Application/DTOs/ArtifactDto.cs` | `StorageUri` 可能暴露內部 S3/MinIO 端點路徑。 |

---

## 2. OWASP Top 10 覆蓋分析

| OWASP | 類別 | 狀態 | 發現數 |
|-------|------|------|--------|
| A01 | Broken Access Control | 🔴 不合格 | 3 |
| A02 | Cryptographic Failures | 🟡 需改善 | 2 |
| A03 | Injection | 🔴 不合格 | 2 |
| A04 | Insecure Design | 🟡 需改善 | 4 |
| A05 | Security Misconfiguration | 🟡 需改善 | 5 |
| A06 | Vulnerable Components | 🟢 低風險 | 1 |
| A07 | Identification Failures | 🔴 不合格 | 1 |
| A08 | Data Integrity Failures | 🟡 需改善 | 1 |
| A09 | Logging Failures | 🟡 需改善 | 1 |
| A10 | SSRF | 🟡 需改善 | 1 |

---

## 3. 修復建議（依優先級）

### P0 — 上線前必須修復

1. **加入 Auth/AuthZ** — 至少在 API 與 SignalR Hub 加上 JWT Bearer 驗證 + RBAC
2. **修復 Path Traversal** — `LocalStorageAdapter.GetFullPath()` 加入路徑正規化與邊界檢查
3. **修復 Command Injection** — `QemuImgService` 改用 `ProcessStartInfo.ArgumentList`
4. **Saga 持久化** — 改用 EF Core 或 Redis Saga Repository

### P1 — 短期修復

5. **DataProtection key 持久化** — 配置 `PersistKeysToDbColumn()` 或 volume mount
6. **nginx 安全標頭** — 加入完整的安全回應標頭
7. **容器非 root** — 所有 Dockerfile 加入 `USER` 指令
8. **輸入驗證** — API endpoint 加入 FluentValidation 或 endpoint filter
9. **移除硬編碼密碼** — 連線字串 fallback 改為 throw

### P2 — 中期改善

10. **CORS 收斂** — 限制合法 Origin/Method/Header
11. **SignalR Group** — 按 Job ID 或 User 限制進度可見範圍
12. **Log injection 防護** — 驗證 Correlation ID 格式
13. **`.dockerignore`** — 排除 `.git`、`node_modules`、測試資料

---

## 4. 合規性摘要

| 合規標準 | 狀態 | 備註 |
|----------|------|------|
| OWASP Top 10 | ❌ 不合格 | A01/A03/A07 有嚴重問題 |
| CIS Docker Benchmark | ⚠️ 部分合格 | 容器以 root 執行 |
| NIST 800-53 (AC) | ❌ 不合格 | 無存取控制 |
| 資料加密靜態 | ⚠️ 實作但不完整 | Key 持久化未配置 |
| 審計追蹤 | ✅ 已實作 | Append-only audit log |

> **結論：** 目前專案處於 MVP / 開發階段，安全機制尚未到位。程式碼架構預留了正確的安全擴充點（`IEncryptionService`、`IAuditLogService`、Middleware pipeline），上線前須依優先級完成修復。
