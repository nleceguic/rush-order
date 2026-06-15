import { useEffect, useRef } from 'react'

interface UseQRSessionOptions {
  onWarning: () => void
  onExpired: () => void
}

const WARN_MS   = 25 * 60 * 1000
const EXPIRE_MS = 30 * 60 * 1000

export function useQRSession({ onWarning, onExpired }: UseQRSessionOptions): void {
  const lastActivityRef = useRef(Date.now())
  const warnedRef       = useRef(false)
  const expiredRef      = useRef(false)

  useEffect(() => {
    const reset = () => {
      if (expiredRef.current) return
      lastActivityRef.current = Date.now()
      warnedRef.current       = false
    }
    const events = ['click', 'touchstart', 'scroll', 'keydown'] as const
    events.forEach((e) => window.addEventListener(e, reset, { passive: true }))
    return () => events.forEach((e) => window.removeEventListener(e, reset))
  }, [])

  useEffect(() => {
    const timer = setInterval(() => {
      if (expiredRef.current) return
      const elapsed = Date.now() - lastActivityRef.current
      if (elapsed >= EXPIRE_MS) {
        expiredRef.current = true
        onExpired()
      } else if (elapsed >= WARN_MS && !warnedRef.current) {
        warnedRef.current = true
        onWarning()
      }
    }, 60_000)
    return () => clearInterval(timer)
  }, [onWarning, onExpired])
}
