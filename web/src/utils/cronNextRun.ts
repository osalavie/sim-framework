/**
 * Compute the next N execution times from a Quartz 6-field cron expression.
 *
 * Cron format: sec min hour dayOfMonth month dayOfWeek
 * - sec:        0-59
 * - min:        0-59
 * - hour:       0-23
 * - dayOfMonth: 1-31
 * - month:      1-12
 * - dayOfWeek:  1-7 (1=Sunday, ..., 7=Saturday)
 *
 * Special tokens: star, ?, N, N-step, N-M, N-M/S, N,M,...
 */

interface CronFieldLimits {
  min: number
  max: number
}

const FIELD_LIMITS: Record<number, CronFieldLimits> = {
  0: { min: 0, max: 59 }, // seconds
  1: { min: 0, max: 59 }, // minutes
  2: { min: 0, max: 23 }, // hours
  3: { min: 1, max: 31 }, // day of month
  4: { min: 1, max: 12 }, // months
  5: { min: 1, max: 7 },  // day of week
}

function parseField(field: string, fieldIndex: number): number[] {
  const { min, max } = FIELD_LIMITS[fieldIndex]!
  const values = new Set<number>()

  if (field === '*' || field === '?') {
    for (let i = min; i <= max; i++) values.add(i)
    return [...values]
  }

  const parts = field.split(',')
  for (const part of parts) {
    if (part.includes('/')) {
      // Range with step: N-M/S or */S
      const [range, stepStr] = part.split('/')
      const step = parseInt(stepStr!, 10)
      if (isNaN(step) || step < 1) continue

      let rangeStart = min
      let rangeEnd = max
      if (range && range !== '*') {
        if (range.includes('-')) {
          const [s, e] = range.split('-')
          rangeStart = parseInt(s!, 10)
          rangeEnd = parseInt(e!, 10)
        } else {
          rangeStart = parseInt(range, 10)
          rangeEnd = max
        }
      }

      if (isNaN(rangeStart) || isNaN(rangeEnd)) continue
      for (let i = rangeStart; i <= rangeEnd; i += step) {
        if (i >= min && i <= max) values.add(i)
      }
    } else if (part.includes('-')) {
      const [s, e] = part.split('-')
      const start = parseInt(s!, 10)
      const end = parseInt(e!, 10)
      if (isNaN(start) || isNaN(end)) continue
      for (let i = start; i <= end; i++) {
        if (i >= min && i <= max) values.add(i)
      }
    } else {
      const n = parseInt(part, 10)
      if (!isNaN(n) && n >= min && n <= max) values.add(n)
    }
  }

  const result = [...values].sort((a, b) => a - b)
  return result.length > 0 ? result : (() => { for (let i = min; i <= max; i++) values.add(i); return [...values] })()
}

/** Find the smallest value in `values` that is >= `target`, or null */
function findNext(values: number[], target: number): number | null {
  for (const v of values) {
    if (v >= target) return v
  }
  return null
}

/** Find the smallest value in `values`, or null */
export interface CronNextRunResult {
  /** ISO 8601 date-time strings */
  times: string[]
  /** Error message if parsing failed */
  error?: string
}

/**
 * Compute the next `count` execution times from a cron expression.
 * @param expression - Quartz 6-field cron expression
 * @param count - Number of next times to compute (default 3)
 * @param from - Starting date (default: now)
 * @param maxIterations - Safety limit to prevent infinite loops
 */
export function getNextRuns(
  expression: string,
  count: number = 3,
  from: Date = new Date(),
  maxIterations: number = 10000,
): CronNextRunResult {
  const parts = expression.trim().split(/\s+/)
  if (parts.length !== 6) {
    return { times: [], error: 'Invalid cron expression: expected 6 fields' }
  }

  // Pre-parse all fields
  const fieldValues: number[][] = []
  for (let i = 0; i < 6; i++) {
    fieldValues.push(parseField(parts[i]!, i))
  }

  const times: string[] = []
  // Start from `from` + 1 second to avoid returning the current exact match
  const current = new Date(from.getTime() + 1000)
  let iterations = 0

  while (times.length < count && iterations < maxIterations) {
    iterations++

    const domField = parts[3]!
    const dowField = parts[5]!
    const domSpecified = domField !== '?' && domField !== '*'
    const dowSpecified = dowField !== '?' && dowField !== '*'

    const months = fieldValues[4]!
    const days = fieldValues[3]!
    const dows = fieldValues[5]!
    const hours = fieldValues[2]!
    const minutes = fieldValues[1]!
    const seconds = fieldValues[0]!

    // Walk forward second-by-second from `current`, jumping to next valid minute when seconds don't match.
    // Use a step based on seconds set: if seconds is *, step 1s; otherwise step to next valid second.
    // But the cleanest correct algorithm: advance to the next candidate second, then validate minute/hour/day/month.

    // The KEY insight: when we advance current to next candidate, we need to re-validate ALL higher fields.
    // Use a cascading helper that increments the lowest non-matching field and resets lower ones.

    const candidate = new Date(current.getTime())

    // Up to ~366 days of searching
    for (let attempt = 0; attempt < maxIterations; attempt++) {
      const y = candidate.getFullYear()
      const mo = candidate.getMonth() + 1
      const d = candidate.getDate()
      const h = candidate.getHours()
      const mi = candidate.getMinutes()
      const s = candidate.getSeconds()

      // Validate month
      if (!months.includes(mo)) {
        // Jump to first day of next valid month
        const nextMo = findNext(months, mo + 1)
        if (nextMo === null) {
          candidate.setFullYear(y + 1, 0, 1)
          candidate.setHours(0, 0, 0, 0)
        } else {
          candidate.setFullYear(y, nextMo - 1, 1)
          candidate.setHours(0, 0, 0, 0)
        }
        continue
      }

      // Validate day
      let dayOk = true
      if (domSpecified) {
        dayOk = days.includes(d)
      }
      if (dowSpecified && dayOk) {
        const jsDow = candidate.getDay() // 0=Sun..6=Sat
        const quartzDow = jsDow === 0 ? 1 : jsDow + 1
        if (!dows.includes(quartzDow)) dayOk = false
      }
      // When only one is specified: domSpecified XOR dowSpecified, the specified one must match
      // When both specified: both must match (already enforced by sequential &&)
      // When neither specified: domField='?'/'*' and dowField='?'/'*' → dayOk stays true

      if (!dayOk) {
        // Jump to next day at 00:00:00
        candidate.setDate(d + 1)
        candidate.setHours(0, 0, 0, 0)
        continue
      }

      // Validate hour
      if (!hours.includes(h)) {
        const nextH = findNext(hours, h + 1)
        if (nextH === null) {
          candidate.setDate(d + 1)
          candidate.setHours(0, 0, 0, 0)
        } else {
          candidate.setHours(nextH, 0, 0, 0)
        }
        continue
      }

      // Validate minute
      if (!minutes.includes(mi)) {
        const nextMi = findNext(minutes, mi + 1)
        if (nextMi === null) {
          candidate.setHours(h + 1, 0, 0, 0)
        } else {
          candidate.setMinutes(nextMi, 0, 0)
        }
        continue
      }

      // Validate second
      if (!seconds.includes(s)) {
        const nextS = findNext(seconds, s + 1)
        if (nextS === null) {
          candidate.setMinutes(mi + 1, 0, 0)
        } else {
          candidate.setSeconds(nextS, 0)
        }
        continue
      }

      // All fields match!
      times.push(candidate.toISOString())
      current.setTime(candidate.getTime() + 1000) // advance past this match
      break
    }
  }

  return { times, error: iterations >= maxIterations ? 'Max iterations reached' : undefined }
}
