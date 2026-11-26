import {
  getApprovalBusinessType,
  getApprovalBusinessTypeOrganizations,
  getApprovalBusinessTypeUsers,
  getApprovalFlowDefinition,
  getApprovalFlowDefinitionId,
  postApprovalFlowDefinition,
  putApprovalFlowDefinitionId,
  deleteApprovalFlowDefinitionId,
  postApprovalFlowDefinitionIdPublish,
  postApprovalFlowDefinitionIdDisable,
  postApprovalFlowDefinitionIdVersion,
  postApprovalFlowDefinitionIdValidate,
  postApprovalInstanceSubmit,
  postApprovalInstanceIdResubmit,
  getApprovalInstanceMyPending,
  getApprovalInstanceMyHandled,
  getApprovalInstanceMySubmitted,
  getApprovalInstanceMyCc,
  getApprovalInstanceCounts,
  getApprovalInstanceId,
  postApprovalInstanceApproveTaskId,
  postApprovalInstanceRejectTaskId,
  postApprovalInstanceTransferTaskId,
  postApprovalInstanceSignTaskId,
  postApprovalInstanceReturnToPreviousTaskId,
  postApprovalInstanceReturnToInitiatorTaskId,
  postApprovalInstanceIdWithdraw,
  postApprovalInstanceIdTerminate,
  postApprovalInstanceIdUrge,
  postApprovalInstanceMarkCcReadCcId,
  getApprovalMonitor,
  getApprovalMonitorId,
  postApprovalMonitorIdTerminate,
  postApprovalInstanceIdUploadAttachment,
  postApprovalInstanceUploadTaskAttachmentTaskId,
  getApprovalInstanceAttachmentDownloadAttachmentId,
} from '@/api/approval'
import type {
  ApprovalAttachmentDto,
  ApprovalBusinessTypeDto,
  ApprovalCcDto,
  ApprovalContextFieldDto,
  ApprovalContextSnapshotDto,
  ApprovalFieldDto,
  ApprovalFieldOptionDto,
  ApprovalFieldValueDto,
  ApprovalFlowDefinitionDto,
  ApprovalFlowDefinitionListDto,
  ApprovalFlowEdgeDto,
  ApprovalFlowNodeDto,
  ApprovalFlowValidationResultDto,
  ApprovalInstanceCountsDto,
  ApprovalInstanceDetailDto,
  ApprovalInstanceListDto,
  ApprovalNodeProgressDto,
  ApprovalOrganizationOptionDto,
  ApprovalRecordDto,
  ApprovalTaskDto,
  ApprovalUserOptionDto,
  CreateApprovalFlowDefinitionDto,
  SaveApprovalFlowEdgeDto,
  SaveApprovalFlowNodeDto,
  SubmitApprovalDto,
  UpdateApprovalFlowDefinitionDto,
} from '@/api/approval'
import type { PageData } from '@/types/api'

/** 网络 DTO 直接复用 OpenAPI 生成 Schema，避免契约漂移。 */
export type {
  ApprovalAttachmentDto,
  ApprovalBusinessTypeDto,
  ApprovalCcDto,
  ApprovalContextFieldDto,
  ApprovalContextSnapshotDto,
  ApprovalFieldDto,
  ApprovalFieldOptionDto,
  ApprovalFieldValueDto,
  ApprovalFlowDefinitionDto,
  ApprovalFlowDefinitionListDto,
  ApprovalFlowEdgeDto,
  ApprovalFlowNodeDto,
  ApprovalFlowValidationResultDto,
  ApprovalInstanceCountsDto,
  ApprovalInstanceDetailDto,
  ApprovalInstanceListDto,
  ApprovalNodeProgressDto,
  ApprovalOrganizationOptionDto,
  ApprovalRecordDto,
  ApprovalTaskDto,
  ApprovalUserOptionDto,
  CreateApprovalFlowDefinitionDto,
  SaveApprovalFlowEdgeDto,
  SaveApprovalFlowNodeDto,
  SubmitApprovalDto,
  UpdateApprovalFlowDefinitionDto,
}

export type ApprovalPageQuery = {
  Keyword?: string
  SkipCount?: number
  MaxResultCount?: number
  Sorting?: string
}

export type ApprovalPendingQuery = ApprovalPageQuery & {
  DefinitionCode?: string
  BusinessType?: string
}

export type ApprovalMonitorQuery = ApprovalPageQuery & {
  Status?: string
  BusinessType?: string
  DefinitionCode?: string
  InitiatorUserId?: string
}

const paged = <T>(data: unknown): PageData<T> => data as PageData<T>

/** 我的审批任务：待办、已办、抄送。 */
export const approvalTaskApi = {
  myPending: (query: ApprovalPendingQuery = {}) =>
    getApprovalInstanceMyPending({ query, throwOnError: true }).then((res) =>
      paged<ApprovalTaskDto>(res.data),
    ),
  myHandled: (query: ApprovalPageQuery = {}) =>
    getApprovalInstanceMyHandled({ query, throwOnError: true }).then((res) =>
      paged<ApprovalTaskDto>(res.data),
    ),
  myCc: (query: ApprovalPageQuery = {}) =>
    getApprovalInstanceMyCc({ query, throwOnError: true }).then((res) =>
      paged<ApprovalCcDto>(res.data),
    ),
  markCcRead: (ccId: string) =>
    postApprovalInstanceMarkCcReadCcId({ path: { ccId }, throwOnError: true }).then(
      (res) => res.data,
    ),
  counts: () =>
    getApprovalInstanceCounts({ throwOnError: true }).then(
      (res) => res.data as ApprovalInstanceCountsDto,
    ),
}

/** 审批实例：发起、查询与审批动作。 */
export const approvalInstanceApi = {
  mySubmitted: (query: ApprovalPageQuery = {}) =>
    getApprovalInstanceMySubmitted({ query, throwOnError: true }).then((res) =>
      paged<ApprovalInstanceListDto>(res.data),
    ),
  detail: (id: string) =>
    getApprovalInstanceId({ path: { id }, throwOnError: true }).then(
      (res) => res.data as ApprovalInstanceDetailDto,
    ),
  submit: (body: SubmitApprovalDto) =>
    postApprovalInstanceSubmit({ body, throwOnError: true }).then(
      (res) => res.data as ApprovalInstanceListDto,
    ),
  resubmit: (id: string, body: SubmitApprovalDto) =>
    postApprovalInstanceIdResubmit({ path: { id }, body, throwOnError: true }).then(
      (res) => res.data as ApprovalInstanceListDto,
    ),
  approve: (taskId: string, comment?: string) =>
    postApprovalInstanceApproveTaskId({
      path: { taskId },
      body: { comment: comment ?? null },
      throwOnError: true,
    }).then((res) => res.data),
  reject: (taskId: string, comment?: string) =>
    postApprovalInstanceRejectTaskId({
      path: { taskId },
      body: { comment: comment ?? null },
      throwOnError: true,
    }).then((res) => res.data),
  transfer: (taskId: string, targetUserId: string, comment?: string) =>
    postApprovalInstanceTransferTaskId({
      path: { taskId },
      body: { targetUserId, comment: comment ?? null },
      throwOnError: true,
    }).then((res) => res.data),
  addSign: (taskId: string, targetUserId: string, position: string, comment?: string) =>
    postApprovalInstanceSignTaskId({
      path: { taskId },
      body: { targetUserId, position, comment: comment ?? null },
      throwOnError: true,
    }).then((res) => res.data),
  returnToPrevious: (taskId: string, comment?: string) =>
    postApprovalInstanceReturnToPreviousTaskId({
      path: { taskId },
      body: { comment: comment ?? null },
      throwOnError: true,
    }).then((res) => res.data),
  returnToInitiator: (taskId: string, comment?: string) =>
    postApprovalInstanceReturnToInitiatorTaskId({
      path: { taskId },
      body: { comment: comment ?? null },
      throwOnError: true,
    }).then((res) => res.data),
  withdraw: (id: string, comment?: string) =>
    postApprovalInstanceIdWithdraw({
      path: { id },
      body: { comment: comment ?? null },
      throwOnError: true,
    }).then((res) => res.data),

  /** 终止实例（发起人的上级；管理权限使用监控端 terminate）。 */
  terminate: (id: string, comment: string) =>
    postApprovalInstanceIdTerminate({
      path: { id },
      body: { comment },
      throwOnError: true,
    }).then((res) => res.data),
  urge: (id: string) =>
    postApprovalInstanceIdUrge({ path: { id }, throwOnError: true }).then((res) => res.data),

  /** 发起人上传实例级附件（仅实例在处理中/已退回时可用）。 */
  uploadAttachment: (instanceId: string, file: File) =>
    postApprovalInstanceIdUploadAttachment({
      path: { id: instanceId },
      body: { file } as never,
      throwOnError: true,
    }).then((res) => res.data as ApprovalAttachmentDto),

  /** 审批人上传附件（挂到具体待办任务）。 */
  uploadTaskAttachment: (taskId: string, file: File) =>
    postApprovalInstanceUploadTaskAttachmentTaskId({
      path: { taskId },
      body: { file } as never,
      throwOnError: true,
    }).then((res) => res.data as ApprovalAttachmentDto),

  /**
   * 下载附件（鉴权代理，非预签名直链）。
   *
   * 契约把响应声明为 JSON，实际是二进制流，因此覆盖 `responseType` 并断言为 Blob。
   */
  downloadAttachment: (attachmentId: string) =>
    getApprovalInstanceAttachmentDownloadAttachmentId({
      path: { attachmentId },
      responseType: 'blob',
      throwOnError: true,
    }).then((res) => res.data as unknown as Blob),
}

/** 流程定义：草稿编辑、发布、停用与升版。 */
export const approvalDefinitionApi = {
  businessTypes: () =>
    getApprovalBusinessType({ throwOnError: true }).then(
      (res) => (res.data ?? []) as ApprovalBusinessTypeDto[],
    ),
  users: (keyword = '', maxResultCount = 20) =>
    getApprovalBusinessTypeUsers({
      query: { keyword, maxResultCount },
      throwOnError: true,
    }).then((res) => (res.data ?? []) as ApprovalUserOptionDto[]),
  organizations: () =>
    getApprovalBusinessTypeOrganizations({ throwOnError: true }).then(
      (res) => (res.data ?? []) as ApprovalOrganizationOptionDto[],
    ),
  getList: () =>
    getApprovalFlowDefinition({ throwOnError: true }).then(
      (res) => (res.data ?? []) as ApprovalFlowDefinitionListDto[],
    ),
  get: (id: string) =>
    getApprovalFlowDefinitionId({ path: { id }, throwOnError: true }).then(
      (res) => res.data as ApprovalFlowDefinitionDto,
    ),
  create: (body: CreateApprovalFlowDefinitionDto) =>
    postApprovalFlowDefinition({ body, throwOnError: true }).then(
      (res) => res.data as ApprovalFlowDefinitionDto,
    ),
  update: (id: string, body: UpdateApprovalFlowDefinitionDto) =>
    putApprovalFlowDefinitionId({ path: { id }, body, throwOnError: true }).then(
      (res) => res.data as ApprovalFlowDefinitionDto,
    ),
  remove: (id: string) =>
    deleteApprovalFlowDefinitionId({ path: { id }, throwOnError: true }).then((res) => res.data),
  publish: (id: string) =>
    postApprovalFlowDefinitionIdPublish({ path: { id }, throwOnError: true }).then(
      (res) => res.data as ApprovalFlowDefinitionDto,
    ),
  disable: (id: string) =>
    postApprovalFlowDefinitionIdDisable({ path: { id }, throwOnError: true }).then(
      (res) => res.data as ApprovalFlowDefinitionDto,
    ),
  newVersion: (id: string) =>
    postApprovalFlowDefinitionIdVersion({ path: { id }, throwOnError: true }).then(
      (res) => res.data as ApprovalFlowDefinitionDto,
    ),

  /** 发布前校验：服务端按图结构规则复核，返回可读问题清单。 */
  validate: (id: string) =>
    postApprovalFlowDefinitionIdValidate({ path: { id }, throwOnError: true }).then(
      (res) => res.data as ApprovalFlowValidationResultDto,
    ),
}

/** 实例监控：管理端全局视图。 */
export const approvalMonitorApi = {
  getList: (query: ApprovalMonitorQuery = {}) =>
    getApprovalMonitor({ query, throwOnError: true }).then((res) =>
      paged<ApprovalInstanceListDto>(res.data),
    ),
  detail: (id: string) =>
    getApprovalMonitorId({ path: { id }, throwOnError: true }).then(
      (res) => res.data as ApprovalInstanceDetailDto,
    ),
  terminate: (id: string, comment: string) =>
    postApprovalMonitorIdTerminate({
      path: { id },
      body: { comment },
      throwOnError: true,
    }).then((res) => res.data),
}
