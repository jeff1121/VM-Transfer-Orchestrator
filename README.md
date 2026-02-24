# VMTO — VM Transfer Orchestrator

企業級虛擬機遷移編排工具，支援 **vSphere → Proxmox VE** 遷移。透過 Saga 編排模式自動完成 VMDK 匯出、磁碟格式轉換、物件儲存上傳、匯入 PVE 與驗證等完整流程，並提供即時進度追蹤與中斷 / 重試機制。

---

## 架構總覽

VMTO 採用 **Clean Architecture + DDD (Domain-Driven Design)** 分層設計，搭配 **Event-driven** 架構處理長時間非同步遷移任務：

- **Domain 層**：聚合根（MigrationJob、Connection、Artifact、License）、值物件、領域事件
- **Application 層**：CQRS 命令 / 查詢、DTO、Port 介面
- **Infrastructure 層**：EF Core 持久化、vSphere / PVE 客戶端、S3 儲存、加密服務
- **API 層**：ASP.NET Core Minimal API，提供 REST 端點與 SignalR 即時推送
- **Worker 層**：MassTransit 消費者 + Saga 狀態機，編排多步驟遷移流程

### C4 Level 1 — System Context

```
                    ┌─────────────┐
                    │   使用者     │
                    │  (瀏覽器)    │
                    └──────┬──────┘
                           │ HTTPS
                           ▼
                    ┌─────────────┐
                    │    VMTO     │
                    │   System    │
                    └──┬───┬───┬──┘
                       │   │   │
            ┌──────────┘   │   └──────────┐
            ▼              ▼              ▼
     ┌────────────┐ ┌────────────┐ ┌────────────┐
     │  vSphere   │ │ Proxmox VE │ │  Storage   │
     │  (來源)    │ │  (目標)    │ │ (MinIO/S3) │
     └────────────┘ └────────────┘ └────────────┘
```

### C4 Level 2 — Container Diagram

```
┌──────────────────────────────────────────────────────────────┐
│                        VMTO System                           │
│                                                              │
│  ┌───────────┐    ┌───────────┐    ┌───────────────────┐     │
│  │  Frontend  │───▶│  API      │◀──▶│   PostgreSQL      │     │
│  │  (Vue 3)  │    │ (ASP.NET) │    │   (持久化)        │     │
│  │  :5173    │    │  :5000    │    └───────────────────┘     │
│  └───────────┘    └─────┬─────┘                              │
│                    SignalR│                                    │
│                         │                                    │
│                    ┌────▼─────┐    ┌───────────────────┐     │
│                    │ RabbitMQ │◀──▶│   Worker           │     │
│                    │ (訊息)   │    │ (MassTransit Saga) │     │
│                    │  :5672   │    └─────┬─────────────┘     │
│                    └──────────┘          │                    │
│                                         ▼                    │
│  ┌───────────┐    ┌──────────┐    ┌───────────────────┐     │
│  │   Redis   │    │ Hangfire │    │   MinIO (S3)      │     │
│  │  (快取)   │    │ (排程)   │    │   (產物儲存)      │     │
│  │  :6379    │    └──────────┘    │   :9000           │     │
│  └───────────┘                    └───────────────────┘     │
└──────────────────────────────────────────────────────────────┘
```

---

## 技術棧

| 類別 | 技術 |
|------|------|
| **Backend** | .NET 10, ASP.NET Core Minimal API, Entity Framework Core, MassTransit + RabbitMQ, Hangfire |
| **Frontend** | Vue 3 + TypeScript + Vite, Pinia, Vue Router, SignalR Client |
| **資料庫** | PostgreSQL 17, Redis 7 |
| **儲存** | MinIO (S3 相容), Ceph (可選) |
| **Observability** | OpenTelemetry, Serilog, Prometheus |
| **部署** | Docker Compose, Kubernetes (Helm Chart) |

---

## 快速開始

### 前置需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) 與 npm
- [Docker](https://www.docker.com/) 與 Docker Compose

### 1. 啟動基礎設施

```bash
cd infra
cp .env.example .env   # 編輯 .env 調整密碼等設定
docker compose up -d   # 啟動 PostgreSQL, Redis, RabbitMQ, MinIO
```

### 2. 啟動 API

```bash
dotnet run --project src/VMTO.API
```

### 3. 啟動 Worker

```bash
dotnet run --project src/VMTO.Worker
```

### 4. 啟動前端

```bash
cd frontend
npm install
npm run dev
```

### 5. 存取服務

| 服務 | 網址 |
|------|------|
| 前端 | http://localhost:5173 |
| API / Swagger | http://localhost:5000/swagger |
| Health Check | http://localhost:5000/health |
| Hangfire Dashboard | http://localhost:5000/hangfire (開發模式) |
| RabbitMQ Management | http://localhost:15672 |
| MinIO Console | http://localhost:9001 |

---

## 專案結構

```
VM-Transfer-Orchestrator/
├── src/
│   ├── VMTO.Shared/            # 共用型別：Result、ErrorCodes、CorrelationId、Telemetry
│   ├── VMTO.Domain/            # 領域層：聚合根、值物件、領域事件、策略模式
│   │   ├── Aggregates/         #   MigrationJob, Connection, Artifact, License
│   │   ├── Enums/              #   JobStatus, StepStatus
│   │   ├── Events/             #   JobCreated, StepCompleted, StepFailed …
│   │   ├── Strategies/         #   FullCopy, Incremental 遷移策略
│   │   └── ValueObjects/       #   EncryptedSecret, Checksum, StorageTarget
│   ├── VMTO.Application/       # 應用層：CQRS 命令 / 查詢、DTO、Port 介面
│   ├── VMTO.Infrastructure/    # 基礎設施層：EF Core、S3、vSphere / PVE 客戶端、加密
│   │   ├── Clients/            #   VSphereClient, PveClient, Mock 版本
│   │   ├── Security/           #   DataProtectionEncryptionService, AuditLog
│   │   ├── Storage/            #   S3StorageAdapter, LocalStorageAdapter
│   │   └── Telemetry/          #   OpenTelemetry 設定
│   ├── VMTO.API/               # API 層：Minimal API 端點、Middleware
│   │   ├── Endpoints/          #   Job, Connection, Artifact, License 端點
│   │   └── Middleware/         #   GlobalExceptionHandler, CorrelationId
│   ├── VMTO.Worker/            # Worker 層：MassTransit 消費者、Saga 編排
│   │   ├── Consumers/          #   ExportVmdk, ConvertDisk, Upload, Import, Verify
│   │   ├── Messages/           #   訊息定義
│   │   └── Sagas/              #   MigrationJobSaga 狀態機
│   └── VMTO.LicenseServer/     # 授權伺服器（獨立服務）
├── tests/
│   ├── VMTO.Domain.Tests/      # 領域層單元測試
│   ├── VMTO.Application.Tests/ # 應用層單元測試
│   ├── VMTO.Infrastructure.Tests/ # 基礎設施層測試
│   └── VMTO.API.Tests/         # API 整合測試
├── frontend/                   # Vue 3 前端應用
├── infra/                      # Docker Compose 與 Dockerfile
├── helm/                       # Kubernetes Helm Chart
├── docs/                       # 文件
│   ├── adr/                    # 架構決策記錄
│   └── openapi.yaml            # OpenAPI 3.0 規格
├── VMTO.sln                    # .NET Solution
└── Directory.Build.props       # 全域建置屬性 (.NET 10, Nullable, WarningsAsErrors)
```

---

## 建置與測試

```bash
# 建置整個方案
dotnet build VMTO.sln

# 執行所有測試
dotnet test VMTO.sln

# 執行單一測試專案
dotnet test tests/VMTO.Domain.Tests

# 前端建置
cd frontend && npm run build
```

---

## 部署

### Docker Compose（完整環境）

```bash
cd infra
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d
```

此命令會建置 API、Worker、Frontend 容器並連同基礎設施一起啟動。Worker 預設啟動 2 個副本。

### Kubernetes (Helm)

```bash
helm install vmto helm/ -f helm/values-prod.yaml
```

Helm Chart 包含 API、Worker、Frontend 的 Deployment/Service，以及可選的 Ingress 與 HPA 設定。詳見 `helm/values.yaml`。

---

## 選型決策記錄 (ADR)

| 編號 | 標題 | 摘要 |
|------|------|------|
| [ADR-001](docs/adr/001-masstransit-rabbitmq.md) | MassTransit + RabbitMQ | 採用 MassTransit 作為訊息匯流排，取代純 Hangfire 方案。支援 Saga 編排、水平擴充、訊息重試與死信佇列。 |
| [ADR-002](docs/adr/002-hangfire-scheduling.md) | Hangfire 輔助排程 | 保留 Hangfire 處理定期清理、增量同步排程等 cron 類任務，與 MassTransit 互補。 |
| [ADR-003](docs/adr/003-minio-default-storage.md) | MinIO 預設儲存 | 選用 MinIO 作為預設物件儲存，S3 相容 API、Docker Compose 自帶、可無縫切換至 Ceph。 |
| [ADR-004](docs/adr/004-dataprotection-encryption.md) | DataProtection 加密 | 使用 ASP.NET DataProtection 加密連線密碼，預留 Vault / KMS 介面。 |

---

## Mock 模式

在沒有真實 vSphere 或 Proxmox VE 環境的情況下，可啟用 Mock 模式執行完整遷移流程：

1. 在 `infra/.env` 中設定 `MOCK_MODE=true`
2. 或在 `appsettings.Development.json` 中設定對應開關

Mock 模式下，`MockVSphereClient` 與 `MockPveClient` 會模擬匯出 / 匯入操作，回傳模擬進度與假資料，適合用於：
- 開發與除錯前端 UI
- 測試 Saga 編排流程
- CI/CD 管線驗證
- Demo 展示

---

## 授權

本專案採用 [MIT License](LICENSE) 授權。
