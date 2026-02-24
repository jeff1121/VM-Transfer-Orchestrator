# ADR-003: 選用 MinIO 作為預設物件儲存

- **狀態**：已接受
- **日期**：2025-01
- **決策者**：VMTO 團隊

## 背景

遷移流程中產生的 VMDK / QCOW2 等磁碟映像檔案需要中繼儲存空間。這些檔案通常數十 GB 至數百 GB，需要：

- 串流上傳 / 下載（避免全檔載入記憶體）
- Checksum 驗證
- 多 Worker 同時存取
- 產物生命週期管理

### 考量的方案

1. **本地檔案系統**：簡單但無法跨節點共享
2. **NFS**：共享儲存但效能與管理成本高
3. **MinIO**：輕量級 S3 相容物件儲存
4. **Ceph (RGW)**：企業級分散式儲存，S3 相容
5. **雲端 S3**：AWS S3 / GCS 等

## 決策

**選用 MinIO 作為預設物件儲存**，並透過 S3 相容 API 抽象層 (`S3StorageAdapter`) 支援切換至 Ceph 或其他 S3 相容儲存。

- `StorageAdapterFactory` 根據 `StorageTarget` 建立對應的儲存適配器
- 預設使用 MinIO，透過 Docker Compose 自動部署
- 生產環境可無縫切換至 Ceph RGW 或 AWS S3
- 保留 `LocalStorageAdapter` 作為開發 / 測試後備方案

## 後果

### 正面

- **S3 相容**：使用標準 S3 API，任何 S3 相容儲存皆可替換
- **Compose 自帶**：Docker Compose 中直接啟動 MinIO，零額外安裝
- **Web Console**：MinIO Console (`:9001`) 提供直觀的檔案瀏覽與管理
- **輕量級**：單節點 MinIO 資源消耗極低，適合開發與小規模部署
- **可擴展**：生產環境可切換至 Ceph 分散式叢集或雲端 S3

### 負面

- **單節點限制**：預設 MinIO 單節點部署無高可用性（生產環境需叢集化或改用 Ceph）
- **額外服務**：比直接使用本地檔案系統多一個服務需要維護
- **網路開銷**：透過 S3 API 存取比本地檔案系統慢，但串流 API 可緩解大檔案問題
