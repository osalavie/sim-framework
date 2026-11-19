import type { Directive } from 'vue'

type Placement = 'top' | 'bottom'
type TooltipValue = string | number | null | undefined

interface TipState {
  text: string
  placement: Placement
}

const SHOW_DELAY = 140
const EDGE_GAP = 8
const TARGET_GAP = 10
const states = new WeakMap<HTMLElement, TipState>()

let bubble: HTMLDivElement | undefined
let showTimer: number | undefined
let activeTarget: HTMLElement | undefined
let viewportBound = false

const normalize = (value: TooltipValue) => (value === null || value === undefined ? '' : String(value).trim())

const ensureBubble = () => {
  if (bubble) return bubble
  bubble = document.createElement('div')
  bubble.className = 'po-tooltip'
  bubble.setAttribute('role', 'tooltip')
  document.body.appendChild(bubble)
  return bubble
}

const hide = () => {
  if (showTimer !== undefined) {
    window.clearTimeout(showTimer)
    showTimer = undefined
  }
  bubble?.classList.remove('is-visible')
  activeTarget = undefined
}

const place = (target: HTMLElement, state: TipState) => {
  const node = ensureBubble()
  node.textContent = state.text
  const anchor = target.getBoundingClientRect()
  const { width, height } = node.getBoundingClientRect()
  let top = state.placement === 'bottom' ? anchor.bottom + TARGET_GAP : anchor.top - TARGET_GAP - height
  if (top < EDGE_GAP) top = anchor.bottom + TARGET_GAP
  if (top + height > window.innerHeight - EDGE_GAP) {
    top = Math.max(EDGE_GAP, anchor.top - TARGET_GAP - height)
  }
  const left = Math.min(
    Math.max(EDGE_GAP, anchor.left + anchor.width / 2 - width / 2),
    Math.max(EDGE_GAP, window.innerWidth - EDGE_GAP - width),
  )
  node.style.top = `${Math.round(top)}px`
  node.style.left = `${Math.round(left)}px`
}

const show = (target: HTMLElement) => {
  if (!target.isConnected) return
  const state = states.get(target)
  if (!state?.text) return
  place(target, state)
  ensureBubble().classList.add('is-visible')
  activeTarget = target
}

const handleEnter = (event: Event) => {
  const target = event.currentTarget as HTMLElement
  if (!states.get(target)?.text) return
  if (showTimer !== undefined) window.clearTimeout(showTimer)
  showTimer = window.setTimeout(() => show(target), SHOW_DELAY)
}

const handleLeave = () => hide()

const bindViewportListeners = () => {
  if (viewportBound) return
  viewportBound = true
  window.addEventListener('scroll', hide, true)
  window.addEventListener('resize', hide)
}

export const tooltipDirective: Directive<HTMLElement, TooltipValue> = {
  mounted(el, binding) {
    states.set(el, {
      text: normalize(binding.value),
      placement: binding.arg === 'bottom' ? 'bottom' : 'top',
    })
    el.addEventListener('mouseenter', handleEnter)
    el.addEventListener('mouseleave', handleLeave)
    el.addEventListener('focusin', handleEnter)
    el.addEventListener('focusout', handleLeave)
    el.addEventListener('click', handleLeave)
    bindViewportListeners()
  },
  updated(el, binding) {
    const state: TipState = {
      text: normalize(binding.value),
      placement: binding.arg === 'bottom' ? 'bottom' : 'top',
    }
    states.set(el, state)
    if (activeTarget !== el) return
    if (!state.text) hide()
    else place(el, state)
  },
  unmounted(el) {
    states.delete(el)
    el.removeEventListener('mouseenter', handleEnter)
    el.removeEventListener('mouseleave', handleLeave)
    el.removeEventListener('focusin', handleEnter)
    el.removeEventListener('focusout', handleLeave)
    el.removeEventListener('click', handleLeave)
    if (activeTarget === el) hide()
  },
}
