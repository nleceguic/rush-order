import { useState, useEffect, useCallback, useRef } from 'react'

export function useCategoryScroll(categoryIds: string[]) {
  const [activeId, setActiveId] = useState<string | null>(categoryIds[0] ?? null)
  const navRef = useRef<HTMLElement>(null)

  // Update activeId via IntersectionObserver as sections scroll into view
  useEffect(() => {
    if (categoryIds.length === 0) return

    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries.filter((e) => e.isIntersecting)
        if (visible.length === 0) return
        const topmost = visible.reduce((a, b) =>
          a.boundingClientRect.top < b.boundingClientRect.top ? a : b,
        )
        const id = topmost.target.id.replace('cat-section-', '')
        setActiveId(id)
      },
      { rootMargin: '-20% 0px -70% 0px', threshold: 0 },
    )

    for (const id of categoryIds) {
      const el = document.getElementById(`cat-section-${id}`)
      if (el !== null) observer.observe(el)
    }

    return () => observer.disconnect()
  }, [categoryIds])

  // Scroll the active nav button into view horizontally
  useEffect(() => {
    if (activeId === null || navRef.current === null) return
    const btn = navRef.current.querySelector<HTMLElement>(`[data-cat="${activeId}"]`)
    btn?.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' })
  }, [activeId])

  const scrollToCategory = useCallback((id: string) => {
    const el = document.getElementById(`cat-section-${id}`)
    if (el === null) return
    // offset for sticky header (approx 120px)
    const top = el.getBoundingClientRect().top + window.scrollY - 120
    window.scrollTo({ top, behavior: 'smooth' })
  }, [])

  return { activeId, navRef, scrollToCategory }
}
