# ADR-001: 採用 MassTransit + RabbitMQ 作為訊息匯流排

- **狀態**：已接受
- **日期**：2025-01
- **決策者**：VMTO 團隊

## 背景

VMTO 的核心流程是多步驟虛擬機遷移（匯出 → 轉換 → 上傳 → 匯入 → 驗證），每個步驟耗時數分鐘至數小時，且需要支援暫停 / 取消 / 重試。我們需要一套可靠的異步任務編排機制。

### 考量的方案

1. **純 Hangfire**：.NET 原生背景任務框架，支援排程與持久化佇列
2. **MassTransit + RabbitMQ**：訊息匯流排 + 訊息代理，支援 Saga 狀態機
3. **自建佇列**：基於 PostgreSQL 或 Redis 的簡易佇列

### 需求

- 多步驟流程的狀態編排（Saga / Orchestration）
- Worker 水平擴充（多副本消費）
- 訊息重試、死信佇列 (DLQ)
- 步驟間解耦，獨立部署 API 與 Worker
- 未來可擴展至多種遷移來源 / 目標

## 決策

**採用 MassTransit + RabbitMQ** 作為主要訊息匯流排與任務編排框架。

- 使用 MassTransit 的 **Saga 狀態機** 編排遷移步驟（`MigrationJobSaga`）
- 每個步驟對應一個 **Consumer**（`ExportVmdkConsumer`、`ConvertDiskConsumer` 等）
- 透過 RabbitMQ 實現 API 與 Worker 的解耦
- 利用 MassTransit 內建的重試策略與錯誤處理

## 後果

### 正面

- **Saga 編排**：MassTransit Saga 天然支援多步驟流程的狀態追蹤與分支邏輯（暫停、取消、重試）
- **水平擴充**：Worker 副本可自由調整，RabbitMQ 自動負載平衡
- **訊息可靠性**：RabbitMQ 提供持久化佇列、ACK 機制、DLQ
- **步驟解耦**：新增遷移步驟只需新增 Consumer 與 Message，不影響現有流程
- **生態系統**：MassTransit 社群活躍，與 .NET 整合完善

### 負面

- **基礎設施複雜度**：需額外維運 RabbitMQ 服務
- **學習曲線**：團隊需熟悉 MassTransit Saga 概念與 RabbitMQ 管理
- **開發環境**：本地開發需啟動 RabbitMQ（透過 Docker Compose 緩解）
- **除錯難度**：分散式訊息追蹤較同步呼叫複雜（透過 CorrelationId 與 OpenTelemetry 緩解）
