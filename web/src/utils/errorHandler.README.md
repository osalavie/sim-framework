# 错误处理工具 (Error Handler Utilities)

统一的错误处理工具模块，提供一致的错误检测、格式化和消息显示功能。

## 功能特性

- ✅ 检测和忽略请求取消错误
- ✅ 格式化各种类型的错误消息
- ✅ 统一的错误处理和消息显示
- ✅ 成功消息显示
- ✅ 错误处理函数包装器

## API 文档

### `isCancelError(error: unknown): boolean`

检查错误是否是请求取消错误。

**参数：**
- `error` - 要检查的错误对象

**返回值：**
- `boolean` - 如果是取消错误返回 true，否则返回 false

**使用场景：**
- 在 catch 块中过滤取消错误
- 避免向用户显示不必要的错误消息

**示例：**
```typescript
try {
  await api.getData()
} catch (error) {
  if (isCancelError(error)) {
    return // 忽略取消错误
  }
  // 处理其他错误
}
```

---

### `formatErrorMessage(error: unknown, defaultMessage?: string): string`

格式化错误消息为用户友好的字符串。

**参数：**
- `error` - 错误对象
- `defaultMessage` - 默认错误消息（可选，默认为 "操作失败"）

**返回值：**
- `string` - 格式化后的错误消息

**支持的错误类型：**
- AxiosError（包括 ABP 框架错误格式）
- Error 对象
- 字符串
- 包含 message 属性的对象

**示例：**
```typescript
const errorMsg = formatErrorMessage(error, '加载数据失败')
console.log(errorMsg) // "网络连接失败" 或自定义消息
```

---

### `handleError(error: unknown, defaultMessage?: string, options?: ErrorOptions): void`

统一的错误处理函数，自动忽略取消错误并显示格式化的错误消息。

**参数：**
- `error` - 错误对象
- `defaultMessage` - 默认错误消息（可选）
- `options` - 配置选项（可选）
  - `showMessage?: boolean` - 是否显示错误消息（默认 true）
  - `logError?: boolean` - 是否在控制台记录错误（默认 true）
  - `logPrefix?: string` - 控制台日志前缀

**示例：**
```typescript
try {
  await api.deleteUser(id)
} catch (error) {
  handleError(error, '删除用户失败', {
    logPrefix: 'UserService',
    showMessage: true,
    logError: true
  })
}
```

---

### `handleSuccess(message: string): void`

显示成功消息。

**参数：**
- `message` - 成功消息文本

**示例：**
```typescript
await api.createUser(data)
handleSuccess('用户创建成功')
```

---

### `withErrorHandler<T>(fn: T, errorMessage: string, options?: ErrorOptions): T`

创建带错误处理的异步函数包装器。

**参数：**
- `fn` - 要包装的异步函数
- `errorMessage` - 错误消息
- `options` - 配置选项（可选）

**返回值：**
- 包装后的函数，自动处理错误

**示例：**
```typescript
// 包装 API 函数
const safeDeleteUser = withErrorHandler(
  (id: string) => api.deleteUser(id),
  '删除用户失败',
  { logPrefix: 'UserService' }
)

// 使用包装后的函数
await safeDeleteUser(userId)
```

## 使用指南

### 1. 在 Composable 中使用

```typescript
import { ref } from 'vue'
import { isCancelError, handleError, handleSuccess } from '@/utils/errorHandler'
import { userApi } from '@/api/user'

export function useUserData() {
  const loading = ref(false)
  const users = ref([])

  const loadUsers = async () => {
    loading.value = true
    try {
      const response = await userApi.getList()
      users.value = response.items
    } catch (error) {
      if (isCancelError(error)) return
      handleError(error, '加载用户列表失败', { logPrefix: 'UserData' })
    } finally {
      loading.value = false
    }
  }

  const createUser = async (data) => {
    try {
      await userApi.create(data)
      handleSuccess('用户创建成功')
      await loadUsers()
      return true
    } catch (error) {
      handleError(error, '用户创建失败')
      return false
    }
  }

  return { loading, users, loadUsers, createUser }
}
```

### 2. 在组件中使用

```vue
<script setup lang="ts">
import { handleError, handleSuccess } from '@/utils/errorHandler'

const handleDelete = async (id: string) => {
  try {
    await api.delete(id)
    handleSuccess('删除成功')
  } catch (error) {
    handleError(error, '删除失败')
  }
}
</script>
```

### 3. 包装 API 函数

```typescript
import { withErrorHandler } from '@/utils/errorHandler'
import { userApi } from '@/api/user'

// 创建安全的 API 函数
export const safeUserApi = {
  delete: withErrorHandler(
    userApi.delete,
    '删除用户失败',
    { logPrefix: 'UserAPI' }
  ),
  create: withErrorHandler(
    userApi.create,
    '创建用户失败',
    { logPrefix: 'UserAPI' }
  ),
}
```

## 重构建议

### 重构前的代码

```typescript
const loadData = async () => {
  loading.value = true
  try {
    const response = await api.getData()
    data.value = response.items
  } catch (error) {
    // 重复的取消错误检查
    if (!error) return
    const err = error as { name?: string; code?: string }
    if (err.name === 'CanceledError' || err.code === 'ERR_CANCELED') {
      return
    }
    
    // 重复的错误处理
    console.error('Failed to load data:', error)
    try {
      useDiscreteApi.message.error('加载数据失败')
    } catch (e) {
      console.error('Failed to show error message:', e)
    }
  } finally {
    loading.value = false
  }
}
```

### 重构后的代码

```typescript
const loadData = async () => {
  loading.value = true
  try {
    const response = await api.getData()
    data.value = response.items
  } catch (error) {
    if (isCancelError(error)) return
    handleError(error, '加载数据失败', { logPrefix: 'DataService' })
  } finally {
    loading.value = false
  }
}
```

## 优势

1. **代码简洁**：减少重复的错误处理代码
2. **一致性**：统一的错误消息格式和显示方式
3. **可维护性**：集中管理错误处理逻辑
4. **类型安全**：完整的 TypeScript 类型支持
5. **灵活性**：支持自定义配置和扩展

## 相关文件

- `errorHandler.ts` - 主要实现文件
- `errorHandler.example.ts` - 使用示例
- `errorHandler.README.md` - 本文档

## 验证需求

本工具模块满足以下需求：

- ✅ **需求 6.2**: 统一的错误处理和取消错误检测
- ✅ **需求 10.2**: 一致的错误消息显示
- ✅ **需求 10.5**: 区分取消错误和真实错误

## 下一步

1. 在现有的 composables 中使用这些工具（useUserData, useRoleData 等）
2. 在 API 层中使用错误处理工具
3. 在组件中直接使用这些工具
4. 考虑添加更多的错误处理策略（如重试、降级等）
