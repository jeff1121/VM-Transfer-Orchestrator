<script setup lang="ts">
import { RouterView } from 'vue-router'
</script>

<template>
  <div id="app">
    <nav class="sidebar">
      <div class="logo">
        <h2>VMTO</h2>
      </div>
      <RouterLink to="/" class="nav-link">儀表板</RouterLink>
      <RouterLink to="/jobs/new" class="nav-link">新增遷移</RouterLink>
      <RouterLink to="/connections" class="nav-link">連線管理</RouterLink>
      <RouterLink to="/settings" class="nav-link">設定</RouterLink>
    </nav>
    <main class="content">
      <RouterView />
    </main>
  </div>
</template>

<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background: #f5f5f5; }
#app { display: flex; min-height: 100vh; }
.sidebar { width: 220px; background: #1a1a2e; color: white; padding: 20px 0; display: flex; flex-direction: column; }
.logo { padding: 0 20px 20px; border-bottom: 1px solid #333; }
.logo h2 { font-size: 1.4rem; }
.nav-link { display: block; padding: 12px 20px; color: #ccc; text-decoration: none; transition: background 0.2s; }
.nav-link:hover, .nav-link.router-link-active { background: #16213e; color: white; }
.content { flex: 1; padding: 24px; }
</style>
