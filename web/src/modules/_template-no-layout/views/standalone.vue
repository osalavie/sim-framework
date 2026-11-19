<template>
  <div class="standalone-page">
    <nav class="nav">
      <h2>独立应用</h2>
      <button @click="goHome">返回首页</button>
    </nav>

    <main class="main">
      <h1>独立页面</h1>
      <p>这是一个完全独立的页面，有自己的导航和布局</p>
      <p>不依赖全局 Layout，可以完全自定义</p>
    </main>

    <footer class="footer">
      <p>© 2025 独立应用</p>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

const goHome = () => {
  router.push('/')
}
</script>

<style scoped>
.standalone-page {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  background: #f8f9fa;
}

.nav {
  background: #343a40;
  color: white;
  padding: 20px 40px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.nav h2 {
  margin: 0;
}

.nav button {
  padding: 8px 16px;
  background: #007bff;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.3s;
}

.nav button:hover {
  background: #0056b3;
}

.main {
  flex: 1;
  padding: 60px 40px;
  max-width: 1200px;
  margin: 0 auto;
  width: 100%;
}

.main h1 {
  font-size: 36px;
  margin-bottom: 20px;
  color: #343a40;
}

.main p {
  font-size: 18px;
  color: #6c757d;
  margin-bottom: 10px;
  line-height: 1.6;
}

.footer {
  background: #343a40;
  color: white;
  padding: 20px;
  text-align: center;
}

.footer p {
  margin: 0;
}
</style>
