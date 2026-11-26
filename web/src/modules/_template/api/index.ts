import request from '@/utils/request'
import type { PageData } from '@/types/api'

export interface TemplateItem {
  id: string
  [key: string]: unknown
}

export type TemplateQueryParams = Record<string, unknown>
export type TemplateFormData = Omit<TemplateItem, 'id'>

/**
 * 模板模块 API
 *
 * 注意：`/api/template/*` 是脚手架占位路径，无真实后端契约，SDK 中没有对应函数，
 * 因此这里直接使用 request 实例（不含契约类型约束）。
 * 新建业务模块时应改用 @hey-api/openapi-ts 生成的 SDK 函数
 * （src/api/{group}/sdk.gen.ts），而非裸 request。
 */
export const templateApi = {
  /**
   * 获取列表
   */
  getList: (params?: TemplateQueryParams) => {
    return request
      .get<PageData<TemplateItem>>('/api/template/list', { params })
      .then((response) => response.data)
  },

  /**
   * 获取详情
   */
  getDetail: (id: string) => {
    return request.get<TemplateItem>(`/api/template/${id}`).then((response) => response.data)
  },

  /**
   * 创建
   */
  create: (data: TemplateFormData) => {
    return request.post<TemplateItem>('/api/template', data).then((response) => response.data)
  },

  /**
   * 更新
   */
  update: (id: string, data: Partial<TemplateFormData>) => {
    return request
      .put<TemplateItem>(`/api/template/${id}`, data)
      .then((response) => response.data)
  },

  /**
   * 删除
   */
  delete: (id: string) => {
    return request.delete(`/api/template/${id}`).then((response) => response.data)
  },
}
