#!/usr/bin/env node

/**
 * 快速创建新模块的脚本
 *
 * 使用方法：
 * node scripts/create-module.mjs <module-name> [module-title]
 *
 * 示例：
 * node scripts/create-module.mjs product 产品管理
 */

import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

// 获取命令行参数
const args = process.argv.slice(2)
const moduleName = args[0]
const moduleTitle = args[1] || moduleName

if (!moduleName) {
  console.error('❌ 错误：请提供模块名称')
  console.log('使用方法：node scripts/create-module.mjs <module-name> [module-title]')
  console.log('示例：node scripts/create-module.mjs product 产品管理')
  process.exit(1)
}

// 验证模块名称格式（kebab-case）
if (!/^[a-z][a-z0-9-]*$/.test(moduleName)) {
  console.error('❌ 错误：模块名称必须是 kebab-case 格式（小写字母、数字、连字符）')
  console.log('正确示例：product, user-management, order-system')
  process.exit(1)
}

// 转换为 PascalCase
function toPascalCase(str) {
  return str
    .split('-')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join('')
}

const modulePascalCase = toPascalCase(moduleName)
const moduleDir = path.join(__dirname, '../src/modules', moduleName)

// 检查模块是否已存在
if (fs.existsSync(moduleDir)) {
  console.error(`❌ 错误：模块 "${moduleName}" 已存在`)
  process.exit(1)
}

console.log(`🚀 开始创建模块: ${moduleName} (${moduleTitle})`)

// 创建模块目录结构
const dirs = [moduleDir, path.join(moduleDir, 'api'), path.join(moduleDir, 'views')]

dirs.forEach((dir) => {
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true })
    console.log(`✅ 创建目录: ${dir}`)
  }
})

// 创建 index.ts
const indexContent = `import type { ModuleConfig } from '../types'
import { ${moduleName}Routes } from './routes'

/**
 * ${moduleTitle}模块配置
 */
export const ${moduleName}Module: ModuleConfig = {
  id: '${moduleName}',
  name: '${moduleTitle}',
  description: '${moduleTitle}功能模块',
  version: '1.0.0',
  enabled: true,
  routes: ${moduleName}Routes,
}
`

fs.writeFileSync(path.join(moduleDir, 'index.ts'), indexContent)
console.log(`✅ 创建文件: index.ts`)

// 创建 routes.ts
const routesContent = `import type { RouteRecordRaw } from 'vue-router'

/**
 * ${moduleTitle}模块路由配置
 */
export const ${moduleName}Routes: RouteRecordRaw[] = [
  {
    path: '/${moduleName}/list',
    name: '${modulePascalCase}List',
    component: () => import('./views/list.vue'),
    meta: {
      title: '${moduleTitle}列表',
      menu: true,
      tab: true,
      keepAlive: true,
      icon: 'list',
    },
  },
]
`

fs.writeFileSync(path.join(moduleDir, 'routes.ts'), routesContent)
console.log(`✅ 创建文件: routes.ts`)

// 创建 api/index.ts
const apiContent = `import { get, post, put, del } from '@/utils/api'
import type { PageData } from '@/types/api'

/**
 * ${moduleTitle}数据类型
 */
export interface ${modulePascalCase} {
  id: string
  name: string
  creationTime?: string
  lastModificationTime?: string
}

/**
 * ${moduleTitle}表单数据
 */
export interface ${modulePascalCase}FormData {
  name: string
}

/**
 * ${moduleTitle}查询参数
 */
export interface ${modulePascalCase}QueryParams {
  name?: string
  page?: number
  pageSize?: number
}

/**
 * ${moduleTitle} API
 */
export const ${moduleName}Api = {
  /**
   * 获取列表
   */
  getList: (params?: ${modulePascalCase}QueryParams) => {
    return get<PageData<${modulePascalCase}>>('/api/${moduleName}/list', params)
  },

  /**
   * 获取详情
   */
  getDetail: (id: string) => {
    return get<${modulePascalCase}>(\`/api/${moduleName}/\${id}\`)
  },

  /**
   * 创建
   */
  create: (data: ${modulePascalCase}FormData) => {
    return post<${modulePascalCase}>('/api/${moduleName}', data)
  },

  /**
   * 更新
   */
  update: (id: string, data: ${modulePascalCase}FormData) => {
    return put<${modulePascalCase}>(\`/api/${moduleName}/\${id}\`, data)
  },

  /**
   * 删除
   */
  delete: (id: string) => {
    return del<void>(\`/api/${moduleName}/\${id}\`)
  },
}
`

fs.writeFileSync(path.join(moduleDir, 'api', 'index.ts'), apiContent)
console.log(`✅ 创建文件: api/index.ts`)

// 创建 views/list.vue
const viewContent = `<template>
  <div class="${moduleName}-container">
    <n-card title="${moduleTitle}列表">
      <n-space vertical :size="16">
        <!-- 搜索栏 -->
        <n-space>
          <n-input
            v-model:value="searchParams.name"
            placeholder="搜索名称"
            clearable
            @keyup.enter="handleSearch"
          />
          <n-button type="primary" @click="handleSearch">
            <template #icon>
              <n-icon :component="SearchOutline" />
            </template>
            搜索
          </n-button>
          <n-button type="primary" @click="handleAdd">
            添加
          </n-button>
        </n-space>

        <!-- 数据表格 -->
        <n-data-table
          :columns="columns"
          :data="data"
          :loading="loading"
          :pagination="pagination"
          @update:page="handlePageChange"
        />
      </n-space>
    </n-card>

    <!-- 添加/编辑对话框 -->
    <n-modal
      v-model:show="showModal"
      preset="dialog"
      :title="editingId ? '编辑' : '添加'"
      positive-text="确定"
      negative-text="取消"
      @positive-click="handleSubmit"
    >
      <n-form
        ref="formRef"
        :model="formData"
        :rules="rules"
        label-placement="left"
        label-width="80"
      >
        <n-form-item label="名称" path="name">
          <n-input v-model:value="formData.name" placeholder="请输入名称" />
        </n-form-item>
      </n-form>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, h } from 'vue'
import { NButton, NSpace, type DataTableColumns, type FormRules, type FormInst } from 'naive-ui'
import { SearchOutline } from '@vicons/ionicons5'
import { ${moduleName}Api } from '../api'
import type { ${modulePascalCase}, ${modulePascalCase}FormData } from '../api'
import { useDiscreteApi } from '@/hooks/discreteApi'

const { message, dialog } = useDiscreteApi

const loading = ref(false)
const data = ref<${modulePascalCase}[]>([])
const showModal = ref(false)
const editingId = ref<string>()
const formRef = ref<FormInst>()

const searchParams = reactive({
  name: '',
  page: 1,
  pageSize: 10,
})

const formData = reactive<${modulePascalCase}FormData>({
  name: '',
})

const pagination = reactive({
  page: 1,
  pageSize: 10,
  pageCount: 1,
  itemCount: 0,
})

const columns: DataTableColumns<${modulePascalCase}> = [
  { title: 'ID', key: 'id', width: 80 },
  { title: '名称', key: 'name' },
  {
    title: '操作',
    key: 'actions',
    width: 200,
    render: (row) => {
      return [
        h(
          NButton,
          {
            size: 'small',
            onClick: () => handleEdit(row),
          },
          { default: () => '编辑' }
        ),
        h(
          NButton,
          {
            size: 'small',
            type: 'error',
            style: 'margin-left: 8px',
            onClick: () => handleDelete(row.id),
          },
          { default: () => '删除' }
        ),
      ]
    },
  },
]

const rules: FormRules = {
  name: [
    { required: true, message: '请输入名称', trigger: 'blur' },
  ],
}

const loadData = async () => {
  loading.value = true
  try {
    const res = await ${moduleName}Api.getList(searchParams)
    data.value = res.items
    pagination.page = res.page
    pagination.pageSize = res.pageSize
    pagination.pageCount = res.totalPages
    pagination.itemCount = res.totalCount
  } catch (error: any) {
    message.error(error.message || '加载数据失败')
  } finally {
    loading.value = false
  }
}

const handleSearch = () => {
  searchParams.page = 1
  loadData()
}

const handlePageChange = (page: number) => {
  searchParams.page = page
  loadData()
}

const handleAdd = () => {
  editingId.value = undefined
  formData.name = ''
  showModal.value = true
}

const handleEdit = (row: ${modulePascalCase}) => {
  editingId.value = row.id
  formData.name = row.name
  showModal.value = true
}

const handleSubmit = async () => {
  if (!formRef.value) return false

  try {
    await formRef.value.validate()

    if (editingId.value) {
      await ${moduleName}Api.update(editingId.value, formData)
      message.success('更新成功')
    } else {
      await ${moduleName}Api.create(formData)
      message.success('添加成功')
    }

    showModal.value = false
    loadData()
    return true
  } catch (error: any) {
    if (error.message) {
      message.error(error.message)
    }
    return false
  }
}

const handleDelete = (id: string) => {
  dialog.warning({
    title: '确认删除',
    content: '确定要删除这条记录吗？',
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await ${moduleName}Api.delete(id)
        message.success('删除成功')
        loadData()
      } catch (error: any) {
        message.error(error.message || '删除失败')
      }
    },
  })
}

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.${moduleName}-container {
  padding: 20px;
}
</style>
`

fs.writeFileSync(path.join(moduleDir, 'views', 'list.vue'), viewContent)
console.log(`✅ 创建文件: views/list.vue`)

console.log(`\n✨ 模块创建成功！\n`)
console.log(`📝 下一步：`)
console.log(`1. 在 src/config/modules.ts 中注册模块：`)
console.log(`   import { ${moduleName}Module } from '@/modules/${moduleName}'`)
console.log(`   export const modules: ModuleConfig[] = [`)
console.log(`     // ...其他模块`)
console.log(`     ${moduleName}Module,`)
console.log(`   ]`)
console.log(`\n2. 重启开发服务器：`)
console.log(`   npm run dev`)
console.log(`\n3. 访问模块页面：`)
console.log(`   http://localhost:5173/${moduleName}/list`)
console.log(`\n🎉 开始开发吧！`)
