import { useEffect, useRef } from 'react'
import { SkipLink } from 'rush-order-pwa'

// SkipLink is intentionally sr-only until keyboard-focused - its whole
// purpose is the focused state, so we focus it programmatically to show
// the real visual design instead of the (correctly) invisible default.
export function Focused() {
  const wrapRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    wrapRef.current?.querySelector<HTMLAnchorElement>('a')?.focus()
  }, [])

  return (
    <div ref={wrapRef} style={{ position: 'relative', height: 80 }}>
      <SkipLink />
    </div>
  )
}
