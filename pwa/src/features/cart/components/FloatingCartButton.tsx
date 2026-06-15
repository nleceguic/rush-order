import { useEffect, useRef, useState } from 'react'
import { useCartStore } from '@shared/store/cartStore'

interface FloatingCartButtonProps {
  hidden:  boolean
  onClick: () => void
}

export function FloatingCartButton({ hidden, onClick }: FloatingCartButtonProps) {
  const itemCount = useCartStore((s) => s.itemCount())
  const total     = useCartStore((s) => s.total())
  const round     = useCartStore((s) => s.round)

  const [bouncing, setBouncing] = useState(false)
  const prevCountRef            = useRef(0)

  useEffect(() => {
    if (itemCount > prevCountRef.current) {
      setBouncing(true)
      const t = setTimeout(() => setBouncing(false), 600)
      prevCountRef.current = itemCount
      return () => clearTimeout(t)
    }
    prevCountRef.current = itemCount
  }, [itemCount])

  if (itemCount === 0 || hidden) return null

  return (
    <div className="fixed bottom-20 inset-x-0 flex justify-center z-30 px-4 pointer-events-none">
      <button
        onClick={onClick}
        className={`
          pointer-events-auto flex items-center gap-2.5 bg-rush-red text-white font-semibold
          px-5 py-3.5 rounded-full shadow-2xl hover:bg-rush-red-hover active:scale-95
          transition-all duration-150 ${bouncing ? 'animate-bounce' : ''}
        `}
        aria-label={`Ver carrito, ${itemCount} ${itemCount === 1 ? 'ítem' : 'ítems'}`}
      >
        <svg className="h-5 w-5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden>
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
            d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"
          />
        </svg>

        <span>Ver pedido</span>

        {round > 1 && (
          <span className="bg-white/30 text-white text-[11px] font-bold px-1.5 py-0.5 rounded-full">
            R{round}
          </span>
        )}

        <span className="bg-white/25 rounded-full px-2 py-0.5 text-sm font-bold tabular-nums">
          {itemCount} · {total.toFixed(2)} €
        </span>
      </button>
    </div>
  )
}
