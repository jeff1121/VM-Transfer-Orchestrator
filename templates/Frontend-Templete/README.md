# Hyper-Modern Glassmorphic & Neumorphic Dashboard Template (`Frontend-Templete`)

這是一個開箱即用的現代化前端企業級 Dashboard 範本套件包，採用 **Vue 3 + TypeScript + Vite + Pinia + Vue-i18n + ECharts** 技術棧，內建完整 **毛玻璃（Glassmorphism）與新擬態（Neumorphism）** 設計系統。

---

## 🌟 範本核心特色

1. **視覺設計體系 (Design Tokens)**：
   - 支援淺色（Light）與深色（Dark）主題無縫切換。
   - 多層次環境光暈背景（Ambient Radial Mesh Gradients）。
   - 真實毛玻璃濾鏡（`backdrop-filter: blur(20px~24px)`）與雙層微光半透明邊框。
   - 新擬態內凹微浮雕輸入框（`var(--neu-inset)`）與立體懸浮卡片（`var(--neu-shadow)`）。
2. **認證頁面隔離 (Auth Layout Isolation)**：
   - 登入頁面自動隱藏所有導覽列與側邊欄，以中央 3D Logo 與毛玻璃卡片呈現。
3. **動態迷你側邊欄 (Dynamic Mini-Sidebar)**：
   - 預設 76px 簡約圖示模式，滑鼠懸停（Hover）時平滑展開為 260px，支援圖釘（📌）鎖定常駐並持久化記錄於 `localStorage`。
4. **自定義毛玻璃新擬態下拉選單 (`NeuSelect.vue`)**：
   - 徹底告別 OS 原生灰色選單，具備 180° 動態旋轉箭頭、Hover 高亮、已選打勾、圖示支援與 Click-Outside 自動收合。
5. **通知抽屜與 Toast 系統**：
   - 具備毛玻璃 Backdrop 遮罩、Click-Outside 任意點擊關閉、`Esc` 鍵快捷關閉。

---

## 🚀 快速啟動

```bash
# 安裝依賴
npm install

# 啟動開發伺服器
npm run dev

# 建置生產環境
npm run build
```

---

## 📁 目錄結構

```
Frontend-Templete/
├── AGENT_INSTRUCTIONS.md      # 給下一個 AI Coding Agent 的專用指示與 Prompt 藍圖
├── index.html                 # HTML 入口與 Google Fonts / Favicon 配置
├── package.json               # 核心依賴（Vue 3, Pinia, vue-i18n, echarts, signalr）
├── vite.config.ts             # Vite 配置（含 PWA 與路徑別名）
├── tsconfig.json              # TypeScript 嚴格模式編譯配置
└── src/
    ├── styles/tokens.css      # 全站 Glassmorphic & Neumorphic CSS Tokens
    ├── components/common/
    │   └── NeuSelect.vue      # 自定義毛玻璃下拉選單元件
    ├── composables/
    │   └── useTheme.ts        # 主題管理（深/淺色切換與持久化）
    ├── stores/
    │   ├── auth.ts            # 登入狀態管理
    │   └── notifications.ts   # 通知抽屜與 Toast 佇列管理
    ├── locales/
    │   ├── zh-TW.ts           # 繁體中文
    │   └── en-US.ts           # 英文
    ├── router/index.ts        # 路由配置（含 Auth 導航守衛）
    ├── views/
    │   ├── LoginView.vue      # 隔離式毛玻璃登入頁
    │   ├── DashboardView.vue  # ECharts 數據監控儀表板
    │   └── SettingsView.vue   # 語系與主題切換設定頁
    ├── App.vue                # 全域版面配置（Mini-Sidebar + Topbar + Drawer）
    └── main.ts                # Vue 應用程式進入點
```
