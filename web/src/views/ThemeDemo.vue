<template>
  <div class="theme-demo-page">
    <n-space vertical :size="24">
      <!-- 头部 -->
      <n-card title="主题系统演示">
        <template #header-extra>
          <ThemeToggle />
        </template>
        <n-text>当前主题: {{ isDark ? '深色模式' : '浅色模式' }}</n-text>
      </n-card>

      <!-- Naive UI 组件演示 -->
      <n-card title="Naive UI 组件 (自动适配主题)">
        <n-space vertical :size="16">
          <!-- 按钮 -->
          <n-space>
            <n-button>默认</n-button>
            <n-button type="primary">主要</n-button>
            <n-button type="info">信息</n-button>
            <n-button type="success">成功</n-button>
            <n-button type="warning">警告</n-button>
            <n-button type="error">错误</n-button>
          </n-space>

          <!-- 文本 -->
          <n-space vertical>
            <n-text>普通文本</n-text>
            <n-text type="primary">主要文本</n-text>
            <n-text type="info">信息文本</n-text>
            <n-text type="success">成功文本</n-text>
            <n-text type="warning">警告文本</n-text>
            <n-text type="error">错误文本</n-text>
          </n-space>

          <!-- 标签 -->
          <n-space>
            <n-tag>默认</n-tag>
            <n-tag type="primary">主要</n-tag>
            <n-tag type="info">信息</n-tag>
            <n-tag type="success">成功</n-tag>
            <n-tag type="warning">警告</n-tag>
            <n-tag type="error">错误</n-tag>
          </n-space>

          <!-- 输入框 -->
          <n-input placeholder="输入框示例" />

          <!-- 进度条 -->
          <n-progress type="line" :percentage="60" />
        </n-space>
      </n-card>

      <!-- CSS 变量演示 -->
      <n-card title="CSS 变量 (使用主题变量)">
        <n-space vertical :size="16">
          <div class="color-demo-grid">
            <div class="color-item">
              <div class="color-box" style="background-color: var(--theme-primary)"></div>
              <span>主色</span>
            </div>
            <div class="color-item">
              <div class="color-box" style="background-color: var(--theme-success)"></div>
              <span>成功</span>
            </div>
            <div class="color-item">
              <div class="color-box" style="background-color: var(--theme-warning)"></div>
              <span>警告</span>
            </div>
            <div class="color-item">
              <div class="color-box" style="background-color: var(--theme-error)"></div>
              <span>错误</span>
            </div>
          </div>

          <div class="text-demo">
            <p class="text-base">主要文本 (--theme-text-base)</p>
            <p class="text-secondary">次要文本 (--theme-text-secondary)</p>
            <p class="text-tertiary">第三级文本 (--theme-text-tertiary)</p>
          </div>

          <div class="bg-demo">
            <div class="bg-box bg-base">主背景</div>
            <div class="bg-box bg-secondary">次要背景</div>
            <div class="bg-box bg-tertiary">第三级背景</div>
          </div>
        </n-space>
      </n-card>

      <!-- 自定义组件演示 -->
      <n-card title="自定义组件 (使用 useTheme Hook)">
        <div class="custom-component" :style="customStyle">
          <h3>动态样式组件</h3>
          <p>这个组件使用 useTheme Hook 获取主题颜色</p>
          <div class="custom-tags">
            <span class="custom-tag" v-for="tag in ['Vue', 'Naive UI', '主题']" :key="tag">
              {{ tag }}
            </span>
          </div>
        </div>
      </n-card>

      <!-- 颜色变量表 -->
      <n-card title="可用的主题变量">
        <n-table :bordered="false" :single-line="false">
          <thead>
            <tr>
              <th>变量名</th>
              <th>说明</th>
              <th>当前值</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in themeVariables" :key="item.name">
              <td><n-code>{{ item.name }}</n-code></td>
              <td>{{ item.description }}</td>
              <td>
                <div class="color-preview">
                  <div class="color-preview-box" :style="{ backgroundColor: item.value }"></div>
                  <n-text depth="3">{{ item.value }}</n-text>
                </div>
              </td>
            </tr>
          </tbody>
        </n-table>
      </n-card>
    </n-space>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useTheme } from '@/hooks/useTheme'
import { ThemeToggle } from '@/components/ThemeToggle'

const { themeColors, isDark } = useTheme()

const customStyle = computed(() => ({
  backgroundColor: themeColors.value.bgSecondary,
  color: themeColors.value.textBase,
  border: `1px solid ${themeColors.value.border}`,
}))

const themeVariables = computed(() => [
  { name: '--theme-primary', description: '主色', value: themeColors.value.primary },
  { name: '--theme-success', description: '成功色', value: themeColors.value.success },
  { name: '--theme-warning', description: '警告色', value: themeColors.value.warning },
  { name: '--theme-error', description: '错误色', value: themeColors.value.error },
  { name: '--theme-text-base', description: '主要文本', value: themeColors.value.textBase },
  { name: '--theme-text-secondary', description: '次要文本', value: themeColors.value.textSecondary },
  { name: '--theme-text-tertiary', description: '第三级文本', value: themeColors.value.textTertiary },
  { name: '--theme-bg-base', description: '主背景', value: themeColors.value.bgBase },
  { name: '--theme-bg-secondary', description: '次要背景', value: themeColors.value.bgSecondary },
  { name: '--theme-bg-tertiary', description: '第三级背景', value: themeColors.value.bgTertiary },
  { name: '--theme-border', description: '边框', value: themeColors.value.border },
  { name: '--theme-divider', description: '分割线', value: themeColors.value.divider },
])
</script>

<style scoped>
.theme-demo-page {
  padding: 24px;
  max-width: 1200px;
  margin: 0 auto;
}

.color-demo-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 16px;
}

.color-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.color-box {
  width: 80px;
  height: 80px;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.text-demo {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.text-base {
  color: var(--theme-text-base);
  margin: 0;
}

.text-secondary {
  color: var(--theme-text-secondary);
  margin: 0;
}

.text-tertiary {
  color: var(--theme-text-tertiary);
  margin: 0;
}

.bg-demo {
  display: flex;
  gap: 16px;
}

.bg-box {
  flex: 1;
  padding: 16px;
  border-radius: 8px;
  text-align: center;
  border: 1px solid var(--theme-border);
}

.bg-base {
  background-color: var(--theme-bg-base);
  color: var(--theme-text-base);
}

.bg-secondary {
  background-color: var(--theme-bg-secondary);
  color: var(--theme-text-base);
}

.bg-tertiary {
  background-color: var(--theme-bg-tertiary);
  color: var(--theme-text-base);
}

.custom-component {
  padding: 24px;
  border-radius: 12px;
  transition: all 0.3s;
}

.custom-component h3 {
  margin: 0 0 12px 0;
  font-size: 20px;
}

.custom-component p {
  margin: 0 0 16px 0;
  opacity: 0.8;
}

.custom-tags {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.custom-tag {
  padding: 6px 12px;
  background-color: var(--theme-primary);
  color: white;
  border-radius: 4px;
  font-size: 14px;
}

.color-preview {
  display: flex;
  align-items: center;
  gap: 8px;
}

.color-preview-box {
  width: 24px;
  height: 24px;
  border-radius: 4px;
  border: 1px solid var(--theme-border);
}
/* Paper Ocean 换肤：只替换视觉，不改演示逻辑 */
.bg-base,
.bg-secondary,
.bg-tertiary {
  border: var(--po-border) !important;
  border-radius: var(--po-r-lg) !important;
}

.bg-box,
.color-box {
  border: var(--po-border) !important;
  border-radius: var(--po-r) !important;
  box-shadow: var(--po-shadow-card) !important;
}

.custom-component {
  border: var(--po-border);
  border-radius: var(--po-r-lg) !important;
  background: var(--po-paper);
  box-shadow: var(--po-shadow-card);
}

.custom-tag {
  color: var(--po-ink) !important;
  border: var(--po-border);
  border-radius: 999px;
  background: var(--po-coral) !important;
  font-weight: var(--po-weight-bold);
}

.color-preview-box {
  border: var(--po-border) !important;
  border-radius: 8px !important;
}
</style>
