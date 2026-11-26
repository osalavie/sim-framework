export interface CardCover {
  /** 封面图片 URL */
  coverUrl: string
  /** 封面模式 */
  mode?: 'left' | 'right' | 'background'
  /** 封面图片描述 */
  description?: string
}
