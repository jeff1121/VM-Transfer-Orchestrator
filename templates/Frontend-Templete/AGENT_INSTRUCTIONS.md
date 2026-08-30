# AI Coding Agent Directives: Hyper-Modern Dashboard Design System

> **用途：** 本文件是為後續接手的 Coding Agent、架構師或開發人員設計的專屬指導藍圖。當要在新專案中建置 Dashboard 或前端頁面時，請**強制遵循**本規範。

---

## 🎯 核心原則與架構禁忌

1. **嚴禁使用 OS 原生 `<select>`**：
   - 任何下拉選單必須使用 `@/components/common/NeuSelect.vue`，確保毛玻璃浮動快顯清單（Popover）、旋轉箭頭與主題樣式一致。
2. **認證頁面隔離 (Auth Layout Isolation)**：
   - 訪客進入 `/login` 或尚未驗證登入前，**絕對不可暴露**左側 Sidebar 與頂部 Topbar。
   - `App.vue` 透過 `isAuthPage` 計算屬性動態隔離，未登入時僅顯示純淨置中的毛玻璃卡片與環境光暈。
3. **動態迷你側邊欄 (Dynamic Mini-Sidebar)**：
   - 必須支援 76px 簡約圖示模式與 260px 展開模式。
   - 在未釘選狀態下，滑鼠懸停（Hover）時以實體背景色（`var(--bg-surface-solid)`）浮動展開，移開後收合。
   - 釘選狀態（`sidebarPinned`）需持久化至 `localStorage`。
4. **抽屜與彈窗 (Drawers & Modals)**：
   - 右側抽屜展開時必須覆蓋半透明毛玻璃遮罩（`.drawer-backdrop`）。
   - 必須支援 **點擊外部任意處關閉 (Click-Outside)** 與鍵盤 **`Esc` 快捷鍵關閉**。

---

## 🎨 必備 CSS Tokens 引用

所有元件顏色與陰影必須引用 `@/styles/tokens.css` 中的變數：

| 效果類別 | CSS 變數 / 用法 | 說明 |
| :--- | :--- | :--- |
| **毛玻璃卡片** | `background: var(--bg-surface); backdrop-filter: var(--glass-blur); border: var(--glass-border); box-shadow: var(--neu-shadow);` | 標準卡片容器 |
| **輸入框內凹** | `background: var(--bg-surface-elevated); box-shadow: var(--neu-inset);` | 輸入框、搜尋列 |
| **懸浮提升** | `box-shadow: var(--neu-shadow-hover); transform: translateY(-2px);` | 卡片懸停、Popover 選單 |
| **主題色漸層** | `background: var(--primary-gradient); color: #fff;` | 主要按鈕、選中項目 |
| **聚焦呼吸光** | `box-shadow: 0 0 0 3px var(--primary-glow), var(--neu-inset);` | Focus 狀態 |

---

## 💡 下一個專案直接套用的提示詞範例 (Prompt Template)

當在全新專案對 AI Agent 下達指令時，請直接使用以下 Prompt：

```text
請參考 templates/Frontend-Templete（或專案內的 docs/AGENT_INSTRUCTIONS.md）中的設計系統與元件結構：
1. 全面遵循 Hyper-Modern Glassmorphism & Neumorphism 設計風格。
2. 使用 @/styles/tokens.css 定義的色碼與陰影。
3. 採用 Auth 佈局隔離、動態迷你側邊欄（76px ⇄ 260px Hover/Pin）與自定義 NeuSelect 下拉元件。
4. 支援深色（Dark）與淺色（Light）主題無縫切換。
```
