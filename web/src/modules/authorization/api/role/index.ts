import {
  getSystemRole,
  getSystemRoleId,
  postSystemRole,
  putSystemRole,
  deleteSystemRoleId,
  postSystemRoleModuleAssignModulesToRoleRoleId,
  getSystemRoleModuleRoleModulesRoleId,
  postSystemRoleAssignRolePermissionsRoleId,
  getSystemPermissionPermissionsByRoleRoleId,
} from '@/api/system'
import type { Role, RoleFormData, RoleQueryParams } from './roleModels'
import type { Permission } from '../permission/permissionModels'
import type { PageData } from '@/types/api'

// 导出类型定义
export * from './roleModels'

/**
 * 角色 API
 * 遵循 userApi 的模式，提供完整的 CRUD 操作
 */
export const roleApi = {
  /**
   * 获取角色列表（分页）
   * GET /api/system/role
   * 支持关键词搜索、排序和分页
   */
  getList: (params?: RoleQueryParams) => {
    return getSystemRole({ query: params as never, throwOnError: true }).then(
      (res) => res.data as PageData<Role>,
    )
  },

  /**
   * 根据 ID 获取角色详情
   * GET /api/system/role/{id}
   */
  get: (id: string) => {
    return getSystemRoleId({ path: { id }, throwOnError: true }).then((res) => res.data as Role)
  },

  /**
   * 创建角色
   * POST /api/system/role
   */
  create: (data: RoleFormData) => {
    return postSystemRole({ body: data, throwOnError: true }).then((res) => res.data as Role)
  },

  /**
   * 更新角色 (ABP的Update方法需要在body中包含id)
   * PUT /api/system/role
   */
  update: (id: string, data: RoleFormData) => {
    return putSystemRole({ body: { ...data, id } as never, throwOnError: true }).then(
      (res) => res.data as Role,
    )
  },

  /**
   * 删除角色
   * DELETE /api/system/role/{id}
   */
  delete: (id: string) => {
    return deleteSystemRoleId({ path: { id }, throwOnError: true }).then((res) => res.data)
  },

  /**
   * 为角色分配模块
   * POST /api/system/role-module/assign-modules-to-role/{roleId}
   */
  assignModules: (roleId: string, moduleIds: string[]) => {
    return postSystemRoleModuleAssignModulesToRoleRoleId({
      path: { roleId },
      body: moduleIds,
      throwOnError: true,
    }).then((res) => res.data)
  },

  /**
   * 获取角色的模块列表
   * GET /api/system/role-module/role-modules/{roleId}
   */
  getRoleModules: (roleId: string) => {
    return getSystemRoleModuleRoleModulesRoleId({ path: { roleId }, throwOnError: true }).then(
      (res) => res.data as string[],
    )
  },

  /**
   * 为角色分配权限
   * POST /api/system/role/assign-role-permissions/{roleId}
   */
  assignPermissions: (roleId: string, permissionIds: string[]) => {
    return postSystemRoleAssignRolePermissionsRoleId({
      path: { roleId },
      body: permissionIds,
      throwOnError: true,
    }).then((res) => res.data)
  },

  /**
   * 获取角色的权限列表
   * GET /api/system/permission/permissions-by-role/{roleId}
   * 后端返回权限 DTO 列表，这里映射为权限 ID 数组以保持与 getRoleModules 一致的契约。
   */
  getRolePermissions: async (roleId: string): Promise<string[]> => {
    const res = await getSystemPermissionPermissionsByRoleRoleId({
      path: { roleId },
      throwOnError: true,
    })
    const permissions = res.data as Permission[]
    return permissions.map((p) => p.id)
  },
}

// 保持向后兼容的别名
export const getRoleList = roleApi.getList
export const getRole = roleApi.get
export const createRole = roleApi.create
export const updateRole = roleApi.update
export const deleteRole = roleApi.delete
