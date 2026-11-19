import {
  getSystemPermission,
  getSystemPermissionId,
  postSystemPermission,
  putSystemPermission,
  deleteSystemPermissionId,
} from '@/api/system'
import type { Permission, PermissionFormData, PermissionQueryParams } from './permissionModels'
import type { PageData } from '@/types/api'

// 导出类型定义
export * from './permissionModels'

/**
 * 权限 API
 * 遵循 userApi 和 roleApi 的模式，提供完整的 CRUD 操作
 */
export const permissionApi = {
  /**
   * 获取权限列表（分页）
   * GET /api/system/permission
   * 支持关键词搜索、排序和分页
   */
  getList: (params?: PermissionQueryParams) => {
    return getSystemPermission({ query: params as never, throwOnError: true }).then(
      (res) => res.data as PageData<Permission>,
    )
  },

  /**
   * 根据 ID 获取权限详情
   * GET /api/system/permission/{id}
   */
  get: (id: string) => {
    return getSystemPermissionId({ path: { id }, throwOnError: true }).then(
      (res) => res.data as Permission,
    )
  },

  /**
   * 创建权限
   * POST /api/system/permission
   */
  create: (data: PermissionFormData) => {
    return postSystemPermission({ body: data as never, throwOnError: true }).then(
      (res) => res.data as Permission,
    )
  },

  /**
   * 更新权限 (ABP的Update方法需要在body中包含id)
   * PUT /api/system/permission
   */
  update: (id: string, data: PermissionFormData) => {
    return putSystemPermission({ body: { ...data, id } as never, throwOnError: true }).then(
      (res) => res.data as Permission,
    )
  },

  /**
   * 删除权限
   * DELETE /api/system/permission/{id}
   */
  delete: (id: string) => {
    return deleteSystemPermissionId({ path: { id }, throwOnError: true }).then((res) => res.data)
  },
}

// 保持向后兼容的别名
export const getPermissionList = permissionApi.getList
export const getPermission = permissionApi.get
export const createPermission = permissionApi.create
export const updatePermission = permissionApi.update
export const deletePermission = permissionApi.delete
