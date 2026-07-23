import { useEffect, type RefObject } from 'react'

const NUDGE_PX = 22
const SPRING_MS = 380
const SPRING = `transform ${SPRING_MS}ms cubic-bezier(0.34, 1.56, 0.64, 1)`
const COOLDOWN_MS = SPRING_MS + 60

/**
 * Nudges `el` a few px in `direction` then springs it back — a rubber-band
 * cue for reaching the start/end of a scroll area. Desktop browsers and
 * Android don't bounce natively the way iOS Safari does, so without this
 * hitting an edge just feels like scrolling silently stops.
 */
function createNudger(el: HTMLElement, axis: 'x' | 'y') {
  let cooling = false
  return (direction: 1 | -1) => {
    if (cooling) return
    cooling = true
    const offset = NUDGE_PX * direction
    el.style.transition = 'none'
    el.style.transform = axis === 'x' ? `translateX(${offset}px)` : `translateY(${offset}px)`
    // Force a style flush so the browser registers the nudge before we spring back.
    el.getBoundingClientRect()
    requestAnimationFrame(() => {
      el.style.transition = SPRING
      el.style.transform = 'translate(0, 0)'
    })
    window.setTimeout(() => { cooling = false }, COOLDOWN_MS)
  }
}

/**
 * Runs `setup(el)` once `ref.current` is attached, even if that happens after
 * this effect's first run (e.g. the element sits behind a loading gate and
 * mounts on a later render of the same component). A plain
 * `useEffect(..., [ref])` only checks `ref.current` once, since the ref
 * object's identity never changes — so it silently never attaches if the
 * node isn't there yet on the first pass. Polling via rAF sidesteps that
 * without requiring callers to track extra dependencies themselves.
 */
function whenAttached(ref: RefObject<HTMLElement>, setup: (el: HTMLElement) => () => void): () => void {
  let rafId = 0
  let teardown: (() => void) | undefined

  const tryAttach = () => {
    const el = ref.current
    if (el === null) {
      rafId = requestAnimationFrame(tryAttach)
      return
    }
    teardown = setup(el)
  }
  tryAttach()

  return () => {
    cancelAnimationFrame(rafId)
    teardown?.()
  }
}

/**
 * Bounces a scrollable element itself when scrolled past its own start/end.
 *
 * Two independent triggers feed the same nudger:
 *  - wheel/touchmove: catches "still pushing" while already sitting at the
 *    edge (slow drags, trackpad).
 *  - scroll: catches *arriving* at the edge, which is the only signal left
 *    once a hard flick hands off to native momentum scrolling — by then the
 *    finger/wheel is done and no more wheel/touchmove events fire, so without
 *    this the bounce silently never played for fast gestures.
 *
 * Stops propagation on the raw input events — this scroller owns its axis,
 * so a gesture that happens over it (e.g. the category tabs) must not also
 * reach an ancestor's bounce listener (e.g. the page's vertical one) and
 * fire a second, unrelated animation for the same gesture.
 */
export function useEdgeBounce(ref: RefObject<HTMLElement>, axis: 'x' | 'y'): void {
  useEffect(() => whenAttached(ref, (el) => {
    const nudge = createNudger(el, axis)
    let touchPos = 0

    const pos = () => (axis === 'x' ? el.scrollLeft : el.scrollTop)
    const max = () => (axis === 'x'
      ? el.scrollWidth - el.clientWidth
      : el.scrollHeight - el.clientHeight)
    const atStart = () => pos() <= 0
    const atEnd = () => pos() >= max() - 1

    // Edges are "arrived at", not "sat at" — once wasAtStart/wasAtEnd is
    // true, further pushes against the same edge don't replay the nudge
    // until the content scrolls away and comes back. Without this, a short
    // list (little/no scrollable range) fires the bounce on nearly every
    // wheel/touch tick, since it's permanently sitting at one edge or both —
    // a rapid, stuttering re-trigger that reads as the page glitching rather
    // than a single rubber-band cue.
    let wasAtStart = atStart()
    let wasAtEnd = atEnd()

    const onWheel = (e: WheelEvent) => {
      e.stopPropagation()
      const delta = axis === 'x' ? e.deltaX : e.deltaY
      if (delta === 0) return
      if (delta < 0 && atStart() && !wasAtStart) nudge(1)
      else if (delta > 0 && atEnd() && !wasAtEnd) nudge(-1)
      wasAtStart = atStart()
      wasAtEnd = atEnd()
    }

    const onTouchStart = (e: TouchEvent) => {
      e.stopPropagation()
      const t = e.touches[0]
      if (t !== undefined) touchPos = axis === 'x' ? t.clientX : t.clientY
    }

    const onTouchMove = (e: TouchEvent) => {
      e.stopPropagation()
      const t = e.touches[0]
      if (t === undefined) return
      const p = axis === 'x' ? t.clientX : t.clientY
      const delta = touchPos - p // positive = dragging content toward the end
      if (delta > 0 && atEnd() && !wasAtEnd) nudge(-1)
      else if (delta < 0 && atStart() && !wasAtStart) nudge(1)
      touchPos = p
      wasAtStart = atStart()
      wasAtEnd = atEnd()
    }

    // Fires continuously during native momentum scrolling too, unlike
    // wheel/touchmove — that's what lets a hard flick still bounce.
    let prevPos = pos()
    const onScroll = () => {
      const p = pos()
      const m = max()
      if (p <= 0 && prevPos > 0) nudge(1)
      else if (p >= m - 1 && prevPos < m - 1) nudge(-1)
      prevPos = p
      wasAtStart = atStart()
      wasAtEnd = atEnd()
    }

    el.addEventListener('wheel', onWheel, { passive: true })
    el.addEventListener('touchstart', onTouchStart, { passive: true })
    el.addEventListener('touchmove', onTouchMove, { passive: true })
    el.addEventListener('scroll', onScroll, { passive: true })
    return () => {
      el.removeEventListener('wheel', onWheel)
      el.removeEventListener('touchstart', onTouchStart)
      el.removeEventListener('touchmove', onTouchMove)
      el.removeEventListener('scroll', onScroll)
    }
  }), [ref, axis])
}

/**
 * Same rubber-band cue, but for whole-page (window) vertical scroll — pages
 * here scroll at the document level rather than through an inner overflow
 * container, so the nudge transform is applied to `ref` (e.g. the product
 * list, scoped to start right below the category tabs) while scroll
 * position is read from `window`. See `useEdgeBounce` for why both
 * wheel/touchmove and `scroll` are needed.
 */
export function useWindowEdgeBounceY(ref: RefObject<HTMLElement>): void {
  useEffect(() => whenAttached(ref, (el) => {
    const nudge = createNudger(el, 'y')
    let touchY = 0

    const atTop = () => window.scrollY <= 0
    const maxY = () => document.documentElement.scrollHeight - window.innerHeight
    const atBottom = () => window.scrollY >= maxY() - 1

    // See useEdgeBounce for why this only fires on arrival, not on every
    // event while already sitting at an edge — short categories barely
    // taller than the viewport otherwise bounce on nearly every scroll tick.
    let wasAtTop = atTop()
    let wasAtBottom = atBottom()

    const onWheel = (e: WheelEvent) => {
      if (e.deltaY < 0 && atTop() && !wasAtTop) nudge(1)
      else if (e.deltaY > 0 && atBottom() && !wasAtBottom) nudge(-1)
      wasAtTop = atTop()
      wasAtBottom = atBottom()
    }

    const onTouchStart = (e: TouchEvent) => { touchY = e.touches[0]?.clientY ?? 0 }
    const onTouchMove = (e: TouchEvent) => {
      const t = e.touches[0]
      if (t === undefined) return
      const delta = touchY - t.clientY // positive = dragging content up (toward the end)
      if (delta > 0 && atBottom() && !wasAtBottom) nudge(-1)
      else if (delta < 0 && atTop() && !wasAtTop) nudge(1)
      touchY = t.clientY
      wasAtTop = atTop()
      wasAtBottom = atBottom()
    }

    let prevY = window.scrollY
    const onScroll = () => {
      const y = window.scrollY
      const m = maxY()
      if (y <= 0 && prevY > 0) nudge(1)
      else if (y >= m - 1 && prevY < m - 1) nudge(-1)
      prevY = y
      wasAtTop = atTop()
      wasAtBottom = atBottom()
    }

    window.addEventListener('wheel', onWheel, { passive: true })
    window.addEventListener('touchstart', onTouchStart, { passive: true })
    window.addEventListener('touchmove', onTouchMove, { passive: true })
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => {
      window.removeEventListener('wheel', onWheel)
      window.removeEventListener('touchstart', onTouchStart)
      window.removeEventListener('touchmove', onTouchMove)
      window.removeEventListener('scroll', onScroll)
    }
  }), [ref])
}
