import { useState, useEffect } from 'react'

export function useColumns(breakpoint = 640): 1 | 2 {
  const [cols, setCols] = useState<1 | 2>(() =>
    typeof window !== 'undefined' && window.innerWidth >= breakpoint ? 2 : 1,
  )

  useEffect(() => {
    const mq = window.matchMedia(`(min-width: ${breakpoint}px)`)
    const handler = (e: MediaQueryListEvent) => setCols(e.matches ? 2 : 1)
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [breakpoint])

  return cols
}
