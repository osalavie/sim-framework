/**
 * 时间格式化工具函数
 */

import { messageKey, t } from '@/i18n'

type RelativeTimeUnit = 'minute' | 'hour' | 'day' | 'month' | 'year'

function translateRelativeTime(unit: RelativeTimeUnit, count: number): string {
  const suffix = count === 1 ? 'Ago' : 'sAgo'
  return t(messageKey('time', `${unit}${suffix}`), { count })
}

/**
 * 格式化日期时间
 * @param date 日期字符串或 Date 对象
 * @param format 格式化模板，默认 'YYYY-MM-DD HH:mm:ss'
 * @returns 格式化后的日期字符串
 */
export function formatDate(date: string | Date, format = 'YYYY-MM-DD HH:mm:ss'): string {
  if (!date) return ''

  const d = typeof date === 'string' ? new Date(date) : date

  if (isNaN(d.getTime())) return ''

  const year = d.getFullYear()
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  const hours = String(d.getHours()).padStart(2, '0')
  const minutes = String(d.getMinutes()).padStart(2, '0')
  const seconds = String(d.getSeconds()).padStart(2, '0')

  return format
    .replace('YYYY', String(year))
    .replace('MM', month)
    .replace('DD', day)
    .replace('HH', hours)
    .replace('mm', minutes)
    .replace('ss', seconds)
}

/**
 * 格式化为相对时间（如：刚刚、5分钟前、3小时前、2天前）
 * @param date 日期字符串或 Date 对象
 * @returns 相对时间字符串
 */
export function formatRelativeTime(date: string | Date): string {
  if (!date) return ''

  const d = typeof date === 'string' ? new Date(date) : date

  if (isNaN(d.getTime())) return ''

  const now = new Date()
  const diff = now.getTime() - d.getTime()
  const seconds = Math.floor(diff / 1000)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)
  const days = Math.floor(hours / 24)
  const months = Math.floor(days / 30)
  const years = Math.floor(days / 365)

  if (seconds < 60) return t('Time:JustNow')
  if (minutes < 60) return translateRelativeTime('minute', minutes)
  if (hours < 24) return translateRelativeTime('hour', hours)
  if (days < 30) return translateRelativeTime('day', days)
  if (months < 12) return translateRelativeTime('month', months)
  return translateRelativeTime('year', years)
}

/**
 * 格式化为友好的时间显示（近期显示相对时间，较早显示具体日期）
 * @param date 日期字符串或 Date 对象
 * @returns 友好的时间字符串
 */
export function formatFriendlyTime(date: string | Date): string {
  if (!date) return ''

  const d = typeof date === 'string' ? new Date(date) : date

  if (isNaN(d.getTime())) return ''

  const now = new Date()
  const diff = now.getTime() - d.getTime()
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))

  // 7天内显示相对时间
  if (days < 7) {
    return formatRelativeTime(date)
  }

  // 今年的显示月-日
  if (d.getFullYear() === now.getFullYear()) {
    return formatDate(date, 'MM-DD')
  }

  // 更早的显示年-月-日
  return formatDate(date, 'YYYY-MM-DD')
}
