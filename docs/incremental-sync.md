# 增量同步架構 (Incremental Sync Architecture)

> **Phase 7** — 本文件描述 VM-Transfer-Orchestrator 增量同步功能的架構設計與逐步落地計畫。

---

## 目錄

1. [增量同步架構概覽](#增量同步架構概覽)
2. [增量同步步驟](#增量同步步驟)
3. [逐步落地計畫](#逐步落地計畫)
4. [Ceph 儲存路線](#ceph-儲存路線)
5. [技術細節](#技術細節)

---

## 增量同步架構概覽

### Full Copy vs Incremental Sync 比較

| 項目               | Full Copy                      | Incremental Sync                   |
| ------------------ | ------------------------------ | ---------------------------------- |
| 資料傳輸量         | 完整磁碟（每次）               | 僅變更區塊（delta）                |
| 停機時間           | 長（傳輸完整磁碟期間需停機）   | 極短（僅最終同步需停機）           |
| 網路頻寬           | 高                             | 低（首次除外）                     |
| 適用場景           | 小型 VM、一次性遷移            | 大型 VM、需最小化停機窗口          |
| 實作複雜度         | 低                             | 中～高                             |
| vSphere 依賴       | VMDK Export API                | VMDK Export API + CBT API          |
| 目前狀態           | ✅ 已實作（`FullCopyStrategy`） | 🔲 規劃中（`IncrementalStrategy`） |

### CBT (Changed Block Tracking) 原理

**vSphere CBT API** 是 VMware 提供的區塊層級變更追蹤機制：

1. **啟用 CBT**：透過 VM `ReconfigVM_Task` 設定 `changeTrackingEnabled = true`
2. **建立 Snapshot**：建立快照時 vSphere 記錄 `changeId`
3. **查詢變更區塊**：呼叫 `QueryChangedDiskAreas(snapshot, disk, startOffset, changeId)` 取得自上次 `changeId` 以來的變更區塊清單
4. **讀取變更資料**：透過 `HttpNfcLease` 僅下載變更區塊的資料
5. **迭代同步**：每次同步後記錄新的 `changeId`，供下次增量使用

```
┌─────────────────┐     CBT Query      ┌─────────────────┐
│   vSphere VM    │ ──────────────────► │  Changed Blocks │
│  (CBT enabled)  │                     │  [0x1000-0x2000]│
│                 │  Download Delta     │  [0x5000-0x5800]│
│                 │ ──────────────────► │  [0xA000-0xA400]│
└─────────────────┘                     └────────┬────────┘
                                                 │
                                                 ▼
                                        ┌─────────────────┐
                                        │  Delta Artifact  │
                                        │  (S3 / Ceph)     │
                                        └────────┬────────┘
                                                 │ Apply
                                                 ▼
                                        ┌─────────────────┐
                                        │  Target Disk     │
                                        │  (PVE / Ceph)    │
                                        └─────────────────┘
```

### 替代方案

| 方案                      | 說明                                           | 優點                 | 缺點                       |
| ------------------------- | ---------------------------------------------- | -------------------- | -------------------------- |
| **vSphere CBT**           | 原生區塊變更追蹤                               | 精確、效能最佳       | 僅限 vSphere               |
| **File-level diff**       | 比對 guest OS 檔案系統差異                     | 平台無關             | 需 agent、不含 metadata    |
| **qemu-img rebase**       | 用 QCOW2 backing file 機制做差異比對           | 標準工具、支援合併   | 需先轉換格式               |
| **rsync-based**           | block-level rsync (`rsync --inplace`)          | 成熟、跨平台         | 需掛載磁碟、效能較差       |

---

## 增量同步步驟

`IncrementalStrategy` 定義五個步驟：

```
EnableCbt → IncrementalPull → ApplyDelta → FinalSyncCutover → Verify
```

### Step 1: EnableCbt — 啟用 vSphere CBT

- 呼叫 `IVSphereClient.EnableCbtAsync(connectionId, vmId)` 啟用 CBT
- 若 CBT 已啟用（`IsCbtEnabledAsync` 回傳 `true`），跳過此步驟
- 啟用後需對 VM 建立 Snapshot + 刪除，觸發 CBT 初始化
- **Consumer**: `EnableCbtConsumer`
- **Message**: `EnableCbtMessage(JobId, StepId, SourceConnectionId, VmId, CorrelationId)`

### Step 2: IncrementalPull — 僅下載 CBT 變更區塊

前提：首次執行時需先完成一次 Full Copy（Initial Full Copy），作為基準磁碟。後續執行才會是真正的增量。

- 呼叫 vSphere CBT API 取得自上次 `changeId` 以來的變更區塊清單
- 僅下載變更區塊資料
- 將 delta 儲存至 S3/Ceph（`jobs/{jobId}/delta/{diskKey}-{changeId}.delta`）
- **Consumer**: `IncrementalPullConsumer`
- **Message**: `IncrementalPullMessage(JobId, StepId, SourceConnectionId, VmId, ChangeId, BaseStorageKey, CorrelationId)`

### Step 3: ApplyDelta — 將 delta 套用至目標磁碟

- 下載 delta artifact 及基準磁碟
- 透過 block-level patching 將 delta 套用至基準磁碟
- 上傳合併後的磁碟
- 支援 `qemu-img rebase`（QCOW2 格式）或自訂 binary patch
- **Consumer**: `ApplyDeltaConsumer`
- **Message**: `ApplyDeltaMessage(JobId, StepId, DeltaStorageKey, TargetStorageKey, CorrelationId)`

### Step 4: FinalSyncCutover — 最終同步 + 切換

此步驟在停機窗口內執行，目標是最小化停機時間：

1. **凍結來源 VM**：暫停或關閉來源 VM（可配置）
2. **最終增量同步**：執行最後一次 CBT 查詢 + delta 下載 + 套用
3. **匯入 PVE**：將最終磁碟狀態匯入 Proxmox VE
4. **啟動目標 VM**：在 PVE 上啟動遷移後的 VM
5. **驗證 DNS / IP**：確認網路配置正確

- **Consumer**: `FinalSyncCutoverConsumer`
- **Message**: `FinalSyncCutoverMessage(JobId, StepId, SourceConnectionId, TargetConnectionId, VmId, PveVmId, CorrelationId)`

### Step 5: Verify — 驗證

與 Full Copy 策略共用 `VerifyConsumer`，驗證磁碟完整性（checksum 比對）。

---

## 逐步落地計畫

```
Phase A (MVP)          Phase B              Phase C               Phase D
┌─────────────┐   ┌─────────────┐   ┌──────────────────┐   ┌──────────────────┐
│ Full Copy    │   │ CBT Enable  │   │ Incremental Pull │   │ Final Sync       │
│ only         │──►│ + Query     │──►│ + Apply Delta    │──►│ & Cutover        │
│ ✅ 已實作    │   │ 驗證 API    │   │ 實作 delta       │   │ 最小停機窗口     │
└─────────────┘   └─────────────┘   └──────────────────┘   └──────────────────┘
```

### Phase A — MVP（目前狀態）

- ✅ `FullCopyStrategy`：ExportVmdk → ConvertDisk → UploadArtifact → ImportToPve → Verify
- ✅ 基本 Saga 協調、重試、通知
- `IncrementalStrategy` 已在 Domain 層定義步驟名稱，但 Consumer 尚未實作

### Phase B — CBT Enable + Query

**目標**：驗證 vSphere CBT API 整合可行性

- [ ] 實作 `EnableCbtConsumer` 完整邏輯
- [ ] 在 `IVSphereClient` 新增 CBT 查詢方法：
  - `QueryChangedDiskAreasAsync(connectionId, vmId, diskKey, changeId)`
  - `CreateSnapshotAsync(connectionId, vmId)`
  - `DeleteSnapshotAsync(connectionId, vmId, snapshotId)`
- [ ] 整合測試：啟用 CBT → 建立快照 → 查詢變更區塊 → 驗證回傳格式
- [ ] 記錄 `changeId` 至 Job metadata

### Phase C — Incremental Pull + Apply Delta

**目標**：實作 delta 下載與套用

- [ ] 實作 `IncrementalPullConsumer` 完整邏輯
- [ ] 實作 `ApplyDeltaConsumer` 完整邏輯
- [ ] 設計 delta 格式（見[技術細節](#delta-格式設計)）
- [ ] 實作斷點續傳機制
- [ ] 效能測試：比較 Full Copy vs Incremental 傳輸時間

### Phase D — Final Sync & Cutover

**目標**：最小停機窗口切換

- [ ] 實作 `FinalSyncCutoverConsumer` 完整邏輯
- [ ] 實作可配置的停機策略（暫停 vs 關機 vs 線上同步）
- [ ] 實作自動 DNS 切換（可選）
- [ ] 實作 rollback 機制（失敗時回滾至來源 VM）
- [ ] E2E 測試：完整增量同步 + 切換流程

---

## Ceph 儲存路線

### 選項比較

| 項目             | Ceph S3 Gateway (RGW)                  | Ceph RBD                              |
| ---------------- | -------------------------------------- | ------------------------------------- |
| **介面**         | S3 相容 API                            | Block device API                      |
| **整合難度**     | 低（與 MinIO 相容，現有 S3 adapter）   | 中～高（需 librbd 或 rbd CLI）        |
| **Delta 支援**   | 存放 delta 檔案（物件）                | 原生 `rbd diff` / `rbd export-diff`   |
| **效能**         | 適合大型物件存取                       | 適合區塊層級隨機讀寫                  |
| **適用場景**     | Phase A~C（MVP 至增量同步）            | Phase D+（進階版、大規模部署）        |
| **PVE 整合**     | 需下載後匯入                           | 可直接掛載為 VM 磁碟                  |

### Ceph S3 Gateway（建議起步方案）

```
Worker ──upload delta──► Ceph RGW (S3 API) ──download──► Worker ──import──► PVE
```

- 直接使用現有 `S3StorageAdapter`，僅需設定 Ceph RGW endpoint
- Delta 以物件方式儲存：`s3://vmto-artifacts/jobs/{jobId}/delta/{diskKey}.delta`
- 優點：零程式碼改動（儲存層面）

### Ceph RBD（進階版）

```
Worker ──rbd import──► Ceph RBD ──rbd diff──► Delta ──rbd import-diff──► Target RBD
                                                                            │
                                                                      PVE attach
```

- 利用 RBD 原生差異匯出：`rbd export-diff --from-snap @base image@current - | ...`
- PVE 可直接使用 Ceph RBD 作為 VM 磁碟儲存後端
- 需新增 `CephRbdStorageAdapter`

---

## 技術細節

### CBT API 呼叫範例

```csharp
// 1. 啟用 CBT
var vmConfigSpec = new VirtualMachineConfigSpec
{
    ChangeTrackingEnabled = true
};
await vm.ReconfigVM_TaskAsync(vmConfigSpec);

// 2. 建立快照（觸發 CBT 初始化）
var snapshotTask = await vm.CreateSnapshot_TaskAsync(
    name: "vmto-cbt-init",
    description: "VMTO CBT initialization snapshot",
    memory: false,
    quiesce: true);

// 3. 查詢變更區塊
var changedAreas = await vm.QueryChangedDiskAreas(
    snapshot: snapshotRef,
    deviceKey: diskDeviceKey,
    startOffset: 0,
    changeId: previousChangeId  // "*" 表示取得所有區塊
);

// changedAreas.ChangedArea[] 包含：
// - Start (long): 變更區塊起始偏移量
// - Length (long): 變更區塊長度
```

### Delta 格式設計

自訂二進位 delta 格式，用於儲存和傳輸變更區塊：

```
┌──────────────────────────────────────────────────┐
│ Header (固定 32 bytes)                           │
├──────────────────────────────────────────────────┤
│ Magic:       "VMTD" (4 bytes)                    │
│ Version:     uint16 (2 bytes)                    │
│ BlockCount:  uint32 (4 bytes)                    │
│ ChangeId:    char[20] (20 bytes)                 │
│ Reserved:    2 bytes                             │
├──────────────────────────────────────────────────┤
│ Block Entry 1                                    │
│   Offset:  int64  (8 bytes)                      │
│   Length:  int32  (4 bytes)                       │
│   Data:    byte[] (Length bytes)                  │
├──────────────────────────────────────────────────┤
│ Block Entry 2                                    │
│   ...                                            │
├──────────────────────────────────────────────────┤
│ Footer                                           │
│   SHA256:  byte[32] (整體校驗碼)                 │
└──────────────────────────────────────────────────┘
```

### 斷點續傳機制

針對大型 delta 傳輸，支援斷點續傳：

1. **分塊上傳**：將 delta 分割為固定大小區塊（預設 64MB），使用 S3 Multipart Upload
2. **進度記錄**：將已上傳的 part 記錄至 Job metadata（`UploadedParts: [1,2,3]`）
3. **續傳邏輯**：重啟時讀取已上傳的 parts，從斷點繼續
4. **下載續傳**：使用 HTTP Range header 從上次中斷位置繼續下載

```csharp
// 斷點續傳虛擬碼
var uploadedParts = await GetUploadedPartsAsync(jobId, deltaKey);
var startPart = uploadedParts.Count;

for (var i = startPart; i < totalParts; i++)
{
    var chunk = await ReadChunkAsync(deltaStream, i, chunkSize);
    await storage.UploadPartAsync(uploadId, i + 1, chunk);
    await SaveUploadProgressAsync(jobId, deltaKey, i);
}

await storage.CompleteMultipartUploadAsync(uploadId);
```

### 排程增量同步（Hangfire）

使用 Hangfire 排程定期增量同步，在正式切換前持續縮小差異：

```csharp
// 建立增量同步排程（每小時執行一次）
RecurringJob.AddOrUpdate<IncrementalSyncJob>(
    $"incremental-sync-{jobId}",
    job => job.ExecuteAsync(jobId, CancellationToken.None),
    Cron.Hourly);

// 切換前移除排程
RecurringJob.RemoveIfExists($"incremental-sync-{jobId}");
```

排程策略可配置：

| 策略         | 排程頻率 | 適用場景                     |
| ------------ | -------- | ---------------------------- |
| Aggressive   | 每 15 分 | 高變更率 VM、需極短停機窗口  |
| Standard     | 每小時   | 一般 VM（預設）              |
| Conservative | 每 6 小時 | 低變更率 VM、頻寬受限環境   |

---

## 相關檔案

| 檔案                                              | 說明                        |
| ------------------------------------------------- | --------------------------- |
| `src/VMTO.Domain/Strategies/IncrementalStrategy.cs`| 增量策略步驟定義             |
| `src/VMTO.Worker/Messages/EnableCbtMessage.cs`     | EnableCbt 訊息              |
| `src/VMTO.Worker/Messages/IncrementalPullMessage.cs`| IncrementalPull 訊息       |
| `src/VMTO.Worker/Messages/ApplyDeltaMessage.cs`   | ApplyDelta 訊息             |
| `src/VMTO.Worker/Messages/FinalSyncCutoverMessage.cs`| FinalSyncCutover 訊息    |
| `src/VMTO.Worker/Consumers/EnableCbtConsumer.cs`   | EnableCbt 消費者（stub）    |
| `src/VMTO.Worker/Consumers/IncrementalPullConsumer.cs`| IncrementalPull 消費者（stub）|
| `src/VMTO.Worker/Consumers/ApplyDeltaConsumer.cs`  | ApplyDelta 消費者（stub）   |
| `src/VMTO.Worker/Consumers/FinalSyncCutoverConsumer.cs`| FinalSyncCutover 消費者（stub）|
