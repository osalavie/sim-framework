/**
 * Core Web Vitals monitoring — collects LCP, FID/INP, CLS, TTFB metrics
 * and reports them via the Performance API.
 *
 * In production, these metrics can be sent to the OpenTelemetry collector
 * or an analytics endpoint.
 */

interface WebVitalMetric {
  name: string
  value: number
  rating: 'good' | 'needs-improvement' | 'poor'
  delta: number
  entries: PerformanceEntry[]
}

/** layout-shift PerformanceEntry 扩展字段（避免 any） */
interface LayoutShiftEntry extends PerformanceEntry {
  hadRecentInput: boolean
  value: number
}

type VitalCallback = (metric: WebVitalMetric) => void

const observers: PerformanceObserver[] = []
let reportedVitals: WebVitalMetric[] = []

function getRating(name: string, value: number): WebVitalMetric['rating'] {
  switch (name) {
    case 'LCP':
      if (value <= 2500) return 'good'
      if (value <= 4000) return 'needs-improvement'
      return 'poor'
    case 'INP':
    case 'FID':
      if (value <= 100) return 'good'
      if (value <= 300) return 'needs-improvement'
      return 'poor'
    case 'CLS':
      if (value <= 0.1) return 'good'
      if (value <= 0.25) return 'needs-improvement'
      return 'poor'
    case 'TTFB':
      if (value <= 800) return 'good'
      if (value <= 1800) return 'needs-improvement'
      return 'poor'
    default:
      return 'good'
  }
}

function createMetric(name: string, value: number, entries: PerformanceEntry[]): WebVitalMetric {
  const rating = getRating(name, value)
  return { name, value, rating, delta: value, entries }
}

/**
 * Observe Largest Contentful Paint (LCP).
 */
function observeLCP(callback: VitalCallback): void {
  if (!('PerformanceObserver' in window)) return

  const observer = new PerformanceObserver((list) => {
    const entries = list.getEntries()
    const lastEntry = entries[entries.length - 1]
    if (lastEntry) {
      const metric = createMetric('LCP', lastEntry.startTime, entries)
      reportedVitals = reportedVitals.filter((v) => v.name !== 'LCP')
      reportedVitals.push(metric)
      callback(metric)
    }
  })
  observer.observe({ type: 'largest-contentful-paint', buffered: true })
  observers.push(observer)
}

/**
 * Observe Interaction to Next Paint (INP) — replaces FID in newer browsers.
 */
function observeINP(callback: VitalCallback): void {
  if (!('PerformanceObserver' in window)) return

  let worstInteraction = 0
  const observer = new PerformanceObserver((list) => {
    for (const entry of list.getEntries()) {
      const duration = entry.duration
      if (duration > worstInteraction) {
        worstInteraction = duration
        const metric = createMetric('INP', duration, [entry])
        callback(metric)
      }
    }
  })
  try {
    observer.observe({ type: 'event', buffered: true })
    observers.push(observer)
  } catch {
    // Fallback to FID for older browsers
    observeFID(callback)
  }
}

/**
 * Observe First Input Delay (FID) — legacy fallback.
 */
function observeFID(callback: VitalCallback): void {
  if (!('PerformanceObserver' in window)) return

  const observer = new PerformanceObserver((list) => {
    for (const entry of list.getEntries()) {
      const eventEntry = entry as PerformanceEventTiming
      const metric = createMetric('FID', eventEntry.processingStart - eventEntry.startTime, [entry])
      callback(metric)
    }
  })
  observer.observe({ type: 'first-input', buffered: true })
  observers.push(observer)
}

/**
 * Observe Cumulative Layout Shift (CLS).
 */
function observeCLS(callback: VitalCallback): void {
  if (!('PerformanceObserver' in window)) return

  let clsValue = 0
  const observer = new PerformanceObserver((list) => {
    for (const entry of list.getEntries()) {
      const layoutShift = entry as LayoutShiftEntry
      if (!layoutShift.hadRecentInput) {
        clsValue += layoutShift.value
        const metric = createMetric('CLS', clsValue, [entry])
        reportedVitals = reportedVitals.filter((v) => v.name !== 'CLS')
        reportedVitals.push(metric)
        callback(metric)
      }
    }
  })
  observer.observe({ type: 'layout-shift', buffered: true })
  observers.push(observer)
}

/**
 * Measure Time to First Byte (TTFB) from Navigation Timing API.
 */
function measureTTFB(callback: VitalCallback): void {
  if (!('performance' in window) || !performance.getEntriesByType) return

  const navEntries = performance.getEntriesByType('navigation')
  if (navEntries.length > 0) {
    const navEntry = navEntries[0] as PerformanceNavigationTiming
    const ttfb = navEntry.responseStart - navEntry.requestStart
    if (ttfb > 0) {
      const metric = createMetric('TTFB', ttfb, [navEntry])
      callback(metric)
    }
  }
}

export interface WebVitalsOptions {
  /** Called when each vital is measured. */
  onMetric?: VitalCallback
  /** Called when the page is being unloaded with all collected vitals. */
  onReport?: (vitals: WebVitalMetric[]) => void
  /** Enable console logging in development. */
  enableLogging?: boolean
}

/**
 * Initialize Core Web Vitals monitoring.
 * Call this once on app startup.
 */
export function initWebVitals(options: WebVitalsOptions = {}): void {
  const { onMetric, onReport, enableLogging = import.meta.env.DEV } = options

  const handleMetric = (metric: WebVitalMetric) => {
    if (enableLogging) {
      console.debug(`[WebVitals] ${metric.name}: ${metric.value.toFixed(2)} (${metric.rating})`)
    }
    onMetric?.(metric)
  }

  measureTTFB(handleMetric)
  observeLCP(handleMetric)
  observeINP(handleMetric)
  observeCLS(handleMetric)

  // Report on page unload
  if (onReport) {
    window.addEventListener('pagehide', () => {
      onReport(reportedVitals)
    }, { once: true })
  }
}

/**
 * Get all reported vitals so far.
 */
export function getReportedVitals(): WebVitalMetric[] {
  return [...reportedVitals]
}

/**
 * Disconnect all observers (for cleanup/testing).
 */
export function disconnectObservers(): void {
  observers.forEach((o) => o.disconnect())
  observers.length = 0
}
