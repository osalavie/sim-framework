# 授权模块 (Authorization Module)

## 概述

授权模块提供完整的 RBAC（基于角色的访问控制）管理功能，包括用户、角色、权限、模块和路由的 CRUD 操作，以及租户管理页面。组织架构与租户管理的菜单由后端路由种子挂在系统管理下，页面与接口仍在本模块维护。

## 功能特性

### 核心功能

- **用户管理**: 创建、编辑、删除和查看用户信息；新建用户使用系统配置的默认密码，编辑不修改密码，支持将用户密码重置为默认密码（需 `system:user:reset-password`，内置账号不可重置）
- **角色管理**: 管理角色并分配模块权限
- **权限管理**: 定义和管理细粒度权限
- **模块管理**: 组织系统功能模块
- **路由管理**: 配置前端路由和访问控制
- **租户管理**: 创建、启停、删除和查看租户
- **批量操作**: 各管理表格支持勾选后批量删除，租户内置 `host`/`default` 不可删除

### 技术特性

- ✅ 响应式设计（桌面/平板/移动端）
- ✅ 表格和卡片双视图模式
- ✅ 搜索、排序、分页功能
- ✅ 表单验证
- ✅ 错误处理
- ✅ 加载状态优化
- ✅ 键盘导航支持
- ✅ 可访问性优化
- ✅ TypeScript 类型安全
- ✅ 用户、角色、权限、模块、路由和租户详情统一展示完整审计信息

## 目录结构

```
authorization/
├── api/                    # API 层
│   ├── user/              # 用户 API
│   ├── role/              # 角色 API
│   ├── permission/        # 权限 API
│   ├── module/            # 模块 API
│   ├── route/             # 路由 API
│   ├── tenant/            # 租户 API
│   └── index.ts           # 统一导出
├── views/                 # 视图层
│   ├── user/              # 用户管理页面
│   ├── role/              # 角色管理页面
│   ├── permission/        # 权限管理页面
│   ├── module/            # 模块管理页面
│   ├── route/             # 路由管理页面
│   └── tenant/            # 租户管理页面
├── utils/                 # 工具函数
├── types/                 # 类型定义
├── constants.ts           # 常量定义
├── routes.ts              # 路由配置
├── index.ts               # 模块配置
├── CODE_QUALITY.md        # 代码质量指南
└── README.md              # 本文件
```

## 快速开始

### 安装依赖

```bash
pnpm install
```

### 开发

```bash
pnpm dev
```

### 构建

```bash
pnpm build
```

## 使用指南

### API 层

每个实体都有对应的 API 模块，提供标准的 CRUD 操作：

```typescript
import { userApi } from '@/modules/authorization/api'

// 获取用户列表
const users = await userApi.getList({
  skipCount: 0,
  maxResultCount: 10,
  keyword: 'search term',
})

// 创建用户（初始密码由系统配置 security.user-default-password 决定）
const newUser = await userApi.create({
  userName: 'john',
  nickName: 'John',
  email: 'john@example.com',
})

// 更新用户（不含密码字段）
await userApi.update(userId, {
  nickName: 'John Doe',
})

// 重置密码为系统默认密码
await userApi.resetUserPassword(userId)

// 删除用户
await userApi.delete(userId)
```

### Composable

使用 composable 管理页面状态和业务逻辑：

```typescript
import { useUserData } from './useUserData'

const {
  loading,
  submitting,
  users,
  searchKeyword,
  pagination,
  loadUsers,
  createUser,
  updateUser,
  resetUserPassword,
  deleteUser,
  handleSearch,
  handlePageChange,
} = useUserData()
```

### 组件

#### 表格视图

```vue
<UserTableView
  :users="users"
  :loading="loading"
  :search-keyword="searchKeyword"
  :pagination="pagination"
  @update:search-keyword="searchKeyword = $event"
  @view="handleView"
  @edit="handleEdit"
  @delete="handleDelete"
  @page-change="handlePageChange"
/>
```

#### 卡片视图

```vue
<UserCardView
  :users="users"
  :loading="loading"
  :search-keyword="searchKeyword"
  :pagination="pagination"
  @update:search-keyword="searchKeyword = $event"
  @view="handleView"
  @edit="handleEdit"
  @delete="handleDelete"
  @page-change="handlePageChange"
/>
```

#### 编辑弹窗

```vue
<UserEditModal
  :show="showEditModal"
  :editing-user="editingUser"
  :submitting="submitting"
  @update:show="showEditModal = $event"
  @submit="handleSubmit"
/>
```

#### 审计信息

用户、角色、权限、模块和路由详情复用全局 `AuditInfo` 组件，统一显示创建时间、创建人、最后修改时间和最后修改人。人员显示名为昵称（空时回退账号名），悬停时用主题气泡显示账号名而非用户 ID。窄屏自动改为单列，长名称会完整换行，不使用省略号截断。

## 开发规范

### 命名规范

- 组件名: PascalCase (如 `UserEditModal.vue`)
- 文件名: 与组件名一致
- Composable: `use` 前缀 (如 `useUserData.ts`)
- 类型文件: `Models` 后缀 (如 `userModels.ts`)

### 代码风格

- 使用 TypeScript
- 使用 Composition API
- 使用 `<script setup>` 语法
- 使用 scoped 样式
- 遵循 ESLint 规则

### 提交规范

```
feat: 添加新功能
fix: 修复 bug
docs: 文档更新
style: 代码格式调整
refactor: 代码重构
perf: 性能优化
test: 测试相关
chore: 构建/工具相关
```

## 性能优化

### 已实现的优化

1. **防抖搜索**: 搜索输入使用 300ms 防抖
2. **请求取消**: 自动取消重复请求
3. **懒加载**: 路由组件懒加载
4. **虚拟滚动**: 大列表使用虚拟滚动
5. **骨架屏**: 加载状态使用骨架屏
6. **缓存**: 常用数据缓存

### 性能建议

- 使用 `v-memo` 优化列表渲染
- 合理使用 `keep-alive` 缓存组件
- 避免不必要的响应式数据
- 使用 `computed` 缓存计算结果

## 可访问性

### 键盘快捷键

- `Ctrl+N`: 新建
- `Ctrl+R`: 刷新
- `Ctrl+F`: 搜索
- `Ctrl+S`: 保存
- `Ctrl+Enter`: 提交表单
- `Esc`: 关闭模态框
- `↑/↓`: 导航列表
- `Enter`: 编辑当前项
- `Delete`: 删除当前项
- `?`: 显示快捷键帮助

### ARIA 支持

- 语义化 HTML
- ARIA 标签
- 屏幕阅读器支持
- 焦点管理

## 响应式设计

### 断点

- Mobile: < 768px
- Tablet: 768px - 1024px
- Desktop: > 1024px

### 适配策略

- 移动端自动切换到卡片视图
- 模态框宽度自适应
- 表单字段垂直堆叠
- 触摸友好的交互

## 测试

### 单元测试

```bash
pnpm test:unit
```

### E2E 测试

```bash
pnpm test:e2e
```

### 测试覆盖率

```bash
pnpm test:coverage
```

## 故障排除

### 常见问题

**Q: 数据加载失败**
A: 检查 API 地址配置和网络连接

**Q: 权限不足**
A: 确认用户有相应的权限

**Q: 表单验证失败**
A: 检查输入格式是否符合要求

**Q: 页面空白**
A: 检查浏览器控制台错误信息

### 调试技巧

1. 使用 Vue DevTools 查看组件状态
2. 检查 Network 面板查看 API 请求
3. 查看 Console 面板的错误信息
4. 使用 Performance 面板分析性能

## 贡献指南

### 提交 PR

1. Fork 项目
2. 创建特性分支
3. 提交代码
4. 编写测试
5. 更新文档
6. 提交 PR

### 代码审查

- 遵循代码规范
- 通过所有测试
- 更新相关文档
- 获得至少一个审查通过

## 更新日志

### v1.0.0 (2024-01-08)

- ✨ 初始版本发布
- ✨ 完整的 CRUD 功能
- ✨ 响应式设计
- ✨ 键盘导航支持
- ✨ 可访问性优化

## 许可证

MIT

## 联系方式

如有问题或建议，请提交 Issue 或 PR。

## 相关资源

- [Vue 3 文档](https://vuejs.org/)
- [Naive UI 文档](https://www.naiveui.com/)
- [TypeScript 文档](https://www.typescriptlang.org/)
- [Vite 文档](https://vitejs.dev/)
