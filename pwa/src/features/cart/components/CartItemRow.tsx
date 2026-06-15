import type { CartItem } from '@shared/types'
import { useCartStore } from '@shared/store/cartStore'

interface CartItemRowProps {
  item:      CartItem
  readOnly?: boolean
}

export function CartItemRow({ item, readOnly = false }: CartItemRowProps) {
  const updateQuantity = useCartStore((s) => s.updateQuantity)
  const removeItem     = useCartStore((s) => s.removeItem)

  return (
    <div className="flex items-start gap-3 py-3">
      <div className="flex-1 min-w-0">
        <div className="flex items-start justify-between gap-2">
          <p className="font-medium text-rush-dark text-sm leading-snug">{item.name}</p>
          <p className="font-semibold text-sm text-rush-dark flex-shrink-0 tabular-nums ml-1">
            {(item.price * item.quantity).toFixed(2)} €
          </p>
        </div>

        {item.modifiers.length > 0 && (
          <div className="mt-0.5 flex flex-wrap gap-x-2 gap-y-0.5">
            {item.modifiers.map((m, idx) => (
              <span key={idx} className="text-[11px] text-gray-400">
                {m.value}{m.price !== 0 ? ` (+${m.price.toFixed(2)} €)` : ''}
              </span>
            ))}
          </div>
        )}

        {item.notes !== undefined && (
          <p className="text-[11px] text-gray-400 mt-0.5 line-clamp-1 italic">"{item.notes}"</p>
        )}
      </div>

      {!readOnly && (
        <div className="flex items-center gap-1.5 flex-shrink-0 mt-0.5">
          <button
            onClick={() => updateQuantity(item.key, item.quantity - 1)}
            className="h-7 w-7 rounded-full border border-gray-200 flex items-center justify-center hover:bg-gray-100 text-base leading-none"
            aria-label="Reducir"
          >−</button>
          <span className="w-5 text-center font-medium text-sm tabular-nums">{item.quantity}</span>
          <button
            onClick={() => updateQuantity(item.key, item.quantity + 1)}
            className="h-7 w-7 rounded-full bg-rush-red text-white flex items-center justify-center hover:bg-rush-red-hover text-base leading-none"
            aria-label="Aumentar"
          >+</button>
          <button
            onClick={() => removeItem(item.key)}
            className="ml-0.5 text-gray-300 hover:text-red-500 transition-colors"
            aria-label={`Eliminar ${item.name}`}
          >
            <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2v10a2 2 0 002 2h8a2 2 0 002-2V6a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zM7 8a1 1 0 012 0v6a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z" clipRule="evenodd" />
            </svg>
          </button>
        </div>
      )}
    </div>
  )
}
