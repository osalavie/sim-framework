<template>
  <div class="custom-layout">
    <header class="custom-header">
      <h1>自定义布局</h1>
      <nav>
        <router-link to="/custom/page1">页面1</router-link>
        <router-link to="/custom/page2">页面2</router-link>
      </nav>
    </header>

    <main class="custom-content">
      <router-view />
    </main>

    <footer class="custom-footer">
      <p>自定义布局 Footer</p>
    </footer>
  </div>
</template>

<script setup lang="ts">
// 自定义布局的逻辑
</script>

<style scoped>
.custom-layout {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  background: var(--theme-bg-secondary);
}

.custom-header {
  background: #2c3e50;
  color: white;
  padding: 20px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.custom-header h1 {
  margin: 0;
  font-size: 24px;
}

.custom-header nav {
  display: flex;
  gap: 20px;
}

.custom-header nav a {
  color: white;
  text-decoration: none;
  padding: 8px 16px;
  border-radius: 4px;
  transition: background 0.3s;
}

.custom-header nav a:hover,
.custom-header nav a.router-link-active {
  background: rgba(255, 255, 255, 0.2);
}

.custom-content {
  flex: 1;
  padding: 20px;
}

.custom-footer {
  background: #34495e;
  color: white;
  padding: 20px;
  text-align: center;
}

.custom-footer p {
  margin: 0;
}
</style>
