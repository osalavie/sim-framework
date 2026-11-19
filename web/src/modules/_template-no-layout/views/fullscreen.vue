<template>
  <div class="fullscreen-page">
    <div class="content">
      <h1>全屏页面</h1>
      <p>这是一个顶级路由，不使用任何布局</p>
      <p>适用于：演示页面、全屏应用、特殊功能页面等</p>

      <div class="actions">
        <button @click="goBack">返回</button>
        <router-link to="/standalone">
          <button>前往独立页面</button>
        </router-link>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

const goBack = () => {
  router.back()
}
</script>

<style scoped>
.fullscreen-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
}

.content {
  text-align: center;
  max-width: 600px;
  padding: 40px;
}

.content h1 {
  font-size: 48px;
  margin-bottom: 20px;
}

.content p {
  font-size: 18px;
  margin-bottom: 10px;
  opacity: 0.9;
}

.actions {
  margin-top: 40px;
  display: flex;
  gap: 20px;
  justify-content: center;
}

button {
  padding: 12px 24px;
  font-size: 16px;
  border: 2px solid white;
  background: transparent;
  color: white;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s;
}

button:hover {
  background: white;
  color: #667eea;
}

a {
  text-decoration: none;
}
</style>
