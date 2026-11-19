import {
  getSystemOrganizationTree,
  getSystemOrganizationId,
  postSystemOrganization,
  putSystemOrganizationId,
  deleteSystemOrganizationId,
  getSystemOrganizationMembersOrganizationUnitId,
  postSystemOrganizationMember,
  deleteSystemOrganizationMemberMembershipId,
  getSystemOrganizationUserOrganizationsUserId,
  getSystemOrganizationDescendantsRootId,
} from '@/api/system'
import type {
  AddMemberDto,
  CreateOrganizationUnitDto,
  OrganizationMemberDto,
  OrganizationUnitDto,
  UpdateOrganizationUnitDto,
} from '@/api/system'

export type {
  AddMemberDto,
  CreateOrganizationUnitDto,
  OrganizationMemberDto,
  OrganizationUnitDto,
  UpdateOrganizationUnitDto,
}

/**
 * 组织架构 API
 * 后端 `OrganizationAppService` 整体受 `system:organization:manage` 权限保护；
 * 成员与企业负责人字段供审批流的「组织负责人」审批人来源解析使用。
 */
export const organizationApi = {
  /** 组织树（扁平列表，含 parentId/path/depth）。 */
  getTree: () =>
    getSystemOrganizationTree({ throwOnError: true }).then(
      (res) => (res.data ?? []) as OrganizationUnitDto[],
    ),

  get: (id: string) =>
    getSystemOrganizationId({ path: { id }, throwOnError: true }).then(
      (res) => res.data as OrganizationUnitDto,
    ),

  create: (body: CreateOrganizationUnitDto) =>
    postSystemOrganization({ body, throwOnError: true }).then(
      (res) => res.data as OrganizationUnitDto,
    ),

  update: (id: string, body: UpdateOrganizationUnitDto) =>
    putSystemOrganizationId({ path: { id }, body, throwOnError: true }).then(
      (res) => res.data as OrganizationUnitDto,
    ),

  remove: (id: string) =>
    deleteSystemOrganizationId({ path: { id }, throwOnError: true }).then((res) => res.data),

  descendants: (rootId: string) =>
    getSystemOrganizationDescendantsRootId({ path: { rootId }, throwOnError: true }).then(
      (res) => (res.data ?? []) as OrganizationUnitDto[],
    ),

  members: (organizationUnitId: string) =>
    getSystemOrganizationMembersOrganizationUnitId({
      path: { organizationUnitId },
      throwOnError: true,
    }).then((res) => (res.data ?? []) as OrganizationMemberDto[]),

  addMember: (body: AddMemberDto) =>
    postSystemOrganizationMember({ body, throwOnError: true }).then(
      (res) => res.data as OrganizationMemberDto,
    ),

  removeMember: (membershipId: string) =>
    deleteSystemOrganizationMemberMembershipId({
      path: { membershipId },
      throwOnError: true,
    }).then((res) => res.data),

  userOrganizations: (userId: string) =>
    getSystemOrganizationUserOrganizationsUserId({
      path: { userId },
      throwOnError: true,
    }).then((res) => (res.data ?? []) as OrganizationMemberDto[]),
}
