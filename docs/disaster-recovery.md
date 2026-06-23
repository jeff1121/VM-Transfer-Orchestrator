# VMTO 災難復原手冊

## 1. 目的

本文件說明 VMTO 在資料毀損、節點故障或配置遺失時的復原流程，目標為：

- 快速恢復 API/Worker 服務
- 還原系統設定（連線、Webhook、Chaos 設定）
- 還原 PostgreSQL 資料並重新啟動任務編排

## 2. 備份項目

### 2.1 系統設定備份（JSON）

- 端點：`POST /api/ops/backup/config`
- 內容：Connections、WebhookSubscriptions、Chaos 設定快照
- 還原：`POST /api/ops/restore/config`

### 2.2 資料庫備份（pg_dump）

- 排程工作：`DatabaseBackupJob`（每日）
- 輸出位置：MinIO `backups` bucket，路徑 `db/YYYY/MM/DD/*.sql`

## 3. 災難復原流程

### 步驟 A：恢復基礎設施

1. 啟動 PostgreSQL、RabbitMQ、MinIO、Redis。
2. 啟動 VMTO API 與 Worker。
3. 確認 `/health/ready` 為健康狀態。

### 步驟 B：還原資料庫

1. 從 MinIO 下載最新 SQL 備份檔。
2. 執行：

```bash
psql "host=<host> port=<port> dbname=<db> user=<user>" < vmto-backup.sql
```

3. 驗證主要資料表（jobs、job_steps、connections、webhook_subscriptions）資料完整。

### 步驟 C：還原系統設定

1. 若已有設定備份 JSON，呼叫：

```http
POST /api/ops/restore/config
```

2. `replaceExisting=true` 時會先清空現有 Connections 與 Webhooks 再匯入。

### 步驟 D：恢復任務處理

1. 呼叫 `GET /api/ops/stuck-jobs` 檢查卡住任務。
2. 對需要修復的任務呼叫 `POST /api/ops/stuck-jobs/{id}/heal`。
3. 確認 Worker queue 消化正常、無大量 DLQ 堆積。

## 4. 驗證清單

- `/api/ops/system-info` 回傳版本與 uptime 正常
- `/api/ops/health-report` 最近快照正常
- `/api/ops/storage-usage` 未超過容量警戒
- Dashboard 任務進度可更新、SignalR 推播正常

## 5. 注意事項

- 請定期演練還原流程，至少每季一次。
- 生產環境建議啟用多區域備份與物件儲存版本化。
- 還原後應立刻執行一次手動備份，建立新的恢復點。
