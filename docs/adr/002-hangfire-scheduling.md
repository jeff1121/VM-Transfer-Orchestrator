# ADR-002: 保留 Hangfire 作為輔助排程引擎

- **狀態**：已接受
- **日期**：2025-01
- **決策者**：VMTO 團隊

## 背景

在 [ADR-001](001-masstransit-rabbitmq.md) 決定採用 MassTransit + RabbitMQ 處理核心遷移流程後，仍有部分任務不適合以訊息驅動方式處理：

- **定期清理**：過期產物 (Artifact) 清理、暫存檔案回收
- **增量同步排程**：排定定期增量遷移任務
- **系統維護**：資料庫清理、健康檢查排程
- **延遲任務**：在特定時間點執行的一次性任務

這些任務具有 cron 排程特性，不需要 Saga 編排。

## 決策

**保留 Hangfire 作為輔助排程引擎**，與 MassTransit 分工互補：

| 職責 | 工具 |
|------|------|
| 多步驟遷移編排（Saga） | MassTransit + RabbitMQ |
| Cron 定期任務 | Hangfire |
| 延遲 / 一次性排程任務 | Hangfire |
| 即時訊息傳遞 | MassTransit |

- Hangfire 使用 PostgreSQL 作為持久化儲存（共用資料庫）
- Hangfire Dashboard 在開發模式下可透過 `/hangfire` 存取
- API 與 Worker 皆啟動 Hangfire Server

## 後果

### 正面

- **職責清晰**：Saga 編排與 cron 排程各有專屬工具，避免用 MassTransit 模擬排程
- **Dashboard 可見性**：Hangfire Dashboard 提供直觀的排程任務監控
- **成熟穩定**：Hangfire 在 .NET 生態系統中久經驗證
- **PostgreSQL 共用**：不需額外儲存基礎設施

### 負面

- **兩套系統**：團隊需同時理解 MassTransit 與 Hangfire 兩套背景任務框架
- **重疊風險**：需明確規範哪些任務使用哪套框架，避免混用
- **資源消耗**：Hangfire Server 會額外佔用少量資源進行輪詢
