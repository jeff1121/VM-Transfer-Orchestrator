# ADR-005: Native AOT 編譯評估

## 狀態

已評估 — 部分採用

## 日期

2025-07

## 背景

.NET 10 持續改進 Native AOT（Ahead-of-Time）編譯的支援範圍，包含更好的 trimming 分析、
更多 BCL API 的 AOT 相容性標注，以及 ASP.NET Core Minimal API 的原生 AOT 支援。

Native AOT 的主要優勢包括：
- **啟動時間大幅縮短**：毫秒等級的冷啟動，適合容器化與 serverless 場景
- **記憶體佔用降低**：無需載入 JIT 編譯器與中繼資料
- **部署體積縮小**：單一原生二進位檔，無需安裝 .NET Runtime
- **可預測的效能**：無 JIT 編譯造成的第一次請求延遲

然而，VMTO 專案使用了大量依賴反射（Reflection）的套件與框架，這些在 Native AOT 模式下
存在不同程度的相容性問題。本 ADR 針對各元件進行 AOT 相容性評估，並制定分層採用策略。

## 分析

### 元件 AOT 相容性評估

| 元件 | AOT 相容性 | 說明 |
|------|-----------|------|
| VMTO.Shared | ✅ 完全相容 | 純值型別（Result、ErrorCode）、常數定義，無反射使用 |
| VMTO.Domain | ✅ 完全相容 | 純 POCO 聚合根與值物件，無任何框架依賴，遵循 DDD 規範 |
| VMTO.Application | ⚠️ 部分相容 | 命令/查詢為 record 型別可相容，但 DI 註冊與泛型 Handler 解析需調整 |
| VMTO.Infrastructure | ❌ 不相容 | EF Core 大量使用反射進行模型建置與變更追蹤；MassTransit 動態建立消費者；DataProtection 動態載入金鑰提供者 |
| VMTO.API | ❌ 不相容 | ASP.NET Core + EF Core + SignalR + Hangfire Dashboard，多重反射依賴 |
| VMTO.Worker | ❌ 不相容 | MassTransit saga 狀態機 + EF Core 持久化 + 動態消費者註冊 |
| VMTO.LicenseServer | ⚠️ 部分相容 | 若簡化為純 Minimal API + 記憶體/檔案儲存，可考慮 AOT 編譯 |

### 主要阻礙因素

1. **EF Core**：截至 .NET 10，EF Core 尚未完整支援 Native AOT。模型建置（Model Building）、
   變更追蹤（Change Tracking）、延遲載入（Lazy Loading）等核心功能仍依賴反射與動態程式碼產生。
   Microsoft 正在進行相關工作，但預計需要數個版本才能完整支援。

2. **MassTransit**：消費者探索（Consumer Discovery）、saga 狀態機配置、訊息反序列化等
   大量使用反射與 `System.Type` 動態操作。目前無 AOT 支援路線圖。

3. **Hangfire**：Job 序列化與反序列化使用 `Type.GetType()` 動態載入，Dashboard 使用
   嵌入式資源與動態 Razor 渲染，與 AOT 根本不相容。

4. **SignalR**：Hub 方法調用依賴反射進行方法解析與參數綁定。雖然 .NET 10 已改善部分場景，
   但搭配強型別 Hub 介面時仍有限制。

### Trimming 相容性作為過渡策略

即使不啟用完整的 Native AOT，標記 `IsTrimmable` 和 `IsAotCompatible` 仍有實際價值：

- **編譯時期警告**：啟用 trimming 分析器，在開發階段就能發現潛在的反射問題
- **套件品質訊號**：標示該程式庫對 trimming 友善，供下游專案參考
- **漸進式準備**：當上層依賴（EF Core 等）支援 AOT 時，底層程式庫已經就緒
- **IL Linker 優化**：即使在 JIT 模式下，標記為可修剪的組件也能在發佈時受益於 IL 修剪

## 決策

1. **目前不啟用 Native AOT 於主要服務**（API、Worker）：EF Core、MassTransit、Hangfire
   的反射依賴使得完整 AOT 編譯不可行，強制啟用只會產生大量執行時期錯誤。

2. **為 Domain/Shared 層啟用 trimming 相容性標記**：這兩個專案無框架依賴，
   可安全標記為 `IsTrimmable=true` 與 `IsAotCompatible=true`，作為 AOT 準備的第一步。

3. **Dockerfile 與部署維持 JIT 模式**：所有容器映像繼續使用標準 .NET Runtime，
   不切換至 AOT 發佈。

4. **持續追蹤上游進展**：密切關注以下里程碑：
   - EF Core AOT 支援（預計 .NET 11 或更後續版本）
   - MassTransit AOT 相容性更新
   - ASP.NET Core Native AOT 功能擴展

5. **LicenseServer 可作為 AOT 試驗場**：若未來需要獨立部署且效能敏感，
   可優先將 LicenseServer 改造為 Native AOT 編譯目標。

## 行動項目

1. ✅ 為 `VMTO.Shared` 加入 `IsTrimmable` 與 `IsAotCompatible` 標記
2. ✅ 為 `VMTO.Domain` 加入 `IsTrimmable` 與 `IsAotCompatible` 標記
3. ✅ 在 `Directory.Build.props` 加入 AOT 就緒狀態註解
4. ⬜ Dockerfile 保持 JIT 模式（現狀維持，無需變更）
5. ⬜ 持續追蹤 .NET AOT 進展，於 .NET 11 發佈時重新評估

## 影響

- **正面**：底層程式庫提前準備好 AOT 相容性，未來遷移成本降低
- **正面**：trimming 分析器可提早發現反射相關問題
- **中性**：對現有建置流程與部署方式無任何影響
- **風險**：極低 — 僅為標記性設定，不改變實際編譯行為

## 參考資料

- [.NET Native AOT Deployment](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [Prepare .NET libraries for trimming](https://learn.microsoft.com/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [ASP.NET Core and Native AOT](https://learn.microsoft.com/aspnet/core/fundamentals/native-aot)
- [EF Core AOT Tracking Issue](https://github.com/dotnet/efcore/issues/29761)
