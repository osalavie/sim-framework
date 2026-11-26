import type { RouteRecordRaw } from 'vue-router'

/**
 * 审批模块路由配置
 *
 * 后端种子已创建同名菜单（`/admin/approval` 与审批中心四视图、流程定义、实例监控），
 * 组件路径按 `src/modules` 相对保存，此处维护同构的静态声明：
 * - todo / handled / submitted / cc 审批中心四视图
 * - definition 流程定义列表、definition/edit 画布式流程设计器
 * - monitor 实例监控、detail 实例详情
 */
export const approvalRoutes: RouteRecordRaw[] = [
  {
    path: 'approval',
    name: 'Approval',
    redirect: { name: 'ApprovalTodo' },
    meta: {
      title: 'Approval:Title',
      menu: true,
      icon: 'carbon:Flow',
      order: 40,
    },
  },
  {
    path: 'approval/todo',
    name: 'ApprovalTodo',
    component: () => import('./views/todo/index.vue'),
    meta: {
      title: 'Approval:Todo:Title',
      menu: true,
      tab: true,
      keepAlive: true,
      order: 1,
    },
  },
  {
    path: 'approval/handled',
    name: 'ApprovalHandled',
    component: () => import('./views/handled/index.vue'),
    meta: {
      title: 'Approval:Handled:Title',
      menu: true,
      tab: true,
      keepAlive: true,
      order: 2,
    },
  },
  {
    path: 'approval/submitted',
    name: 'ApprovalSubmitted',
    component: () => import('./views/submitted/index.vue'),
    meta: {
      title: 'Approval:Submitted:Title',
      menu: true,
      tab: true,
      keepAlive: true,
      order: 3,
    },
  },
  {
    path: 'approval/cc',
    name: 'ApprovalCc',
    component: () => import('./views/cc/index.vue'),
    meta: {
      title: 'Approval:Cc:Title',
      menu: true,
      tab: true,
      keepAlive: true,
      order: 4,
    },
  },
  {
    path: 'approval/definition',
    name: 'ApprovalDefinition',
    component: () => import('./views/definition/index.vue'),
    meta: {
      title: 'Approval:Definition:Title',
      menu: true,
      tab: true,
      keepAlive: true,
      order: 5,
    },
  },
  {
    path: 'approval/monitor',
    name: 'ApprovalMonitor',
    component: () => import('./views/monitor/index.vue'),
    meta: {
      title: 'Approval:Monitor:Title',
      menu: true,
      tab: true,
      keepAlive: true,
      order: 6,
    },
  },
  {
    path: 'approval/detail/:instanceId',
    name: 'ApprovalInstanceDetail',
    component: () => import('./views/detail/index.vue'),
    meta: {
      title: 'Approval:Detail:Title',
      menu: false,
      tab: true,
      keepAlive: false,
      order: 7,
    },
  },
  {
    path: 'approval/definition/edit/:definitionId?',
    name: 'ApprovalDefinitionEdit',
    component: () => import('./views/definition/edit.vue'),
    meta: {
      title: 'Approval:Definition:Edit',
      menu: false,
      tab: true,
      keepAlive: false,
      order: 8,
    },
  },
]
