// ABP 分页响应数据
export interface PageData<T> {
  items: T[]
  totalCount: number
}

// 分页请求参数
export interface PageParams {
  skipCount: number
  maxResultCount: number
}

// 请求配置扩展
export interface RequestConfig {
  showError?: boolean // 是否显示错误提示
  cancelable?: boolean // 是否可取消
}
