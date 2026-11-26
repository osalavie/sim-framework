/**
 * 表单校验失败后的错误定位
 * 字段区内部滚动（弹窗、登录注册、初始化向导）时，把第一个出错的字段滚入视野并聚焦
 */

import { nextTick } from 'vue'
import type { FormInst } from 'naive-ui'
import { prefersReducedMotion } from './accessibility'

const ERROR_FEEDBACK_SELECTOR = '.n-form-item-feedback--error'
const FORM_ITEM_SELECTOR = '.n-form-item'
const FIELD_SELECTOR = [
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[contenteditable="true"]',
].join(',')

type FormTarget = FormInst | HTMLElement | null | undefined

function isVisible(element: HTMLElement): boolean {
  return element.getClientRects().length > 0
}

function resolveFormElement(target: FormTarget): HTMLElement | null {
  if (!target) return null
  if (target instanceof HTMLElement) return target
  // FormInst 类型未声明 $el，但组件实例运行时始终存在
  return (target as FormInst & { $el?: HTMLElement }).$el ?? null
}

/**
 * 校验反馈要到下一个 tick 才渲染，所以定位动作是异步的。
 * 返回是否找到了可见的错误字段。
 */
export async function scrollToFirstFormError(target: FormTarget): Promise<boolean> {
  await nextTick()
  const formElement = resolveFormElement(target)
  if (!formElement) return false

  const feedback = Array.from(
    formElement.querySelectorAll<HTMLElement>(ERROR_FEEDBACK_SELECTOR),
  ).find(isVisible)
  if (!feedback) return false

  const formItem = feedback.closest<HTMLElement>(FORM_ITEM_SELECTOR) ?? feedback
  formItem.scrollIntoView({
    block: 'center',
    inline: 'nearest',
    behavior: prefersReducedMotion() ? 'auto' : 'smooth',
  })

  const field = Array.from(formItem.querySelectorAll<HTMLElement>(FIELD_SELECTOR)).find(isVisible)
  field?.focus({ preventScroll: true })
  return true
}
