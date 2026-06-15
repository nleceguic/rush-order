import { useCartStore } from '@shared/store/cartStore'
import type { CartItem as CartItemType } from '@shared/types'

interface CartItemProps {
  item: CartItemType
}

export function CartItem({ item }: CartItemProps) {
  const { updateQuantity, removeItem } = useCartStore()

  return (
    <div className="flex items-center gap-3 py-3 border-b last:border-0">
      <div className="flex-1 min-w-0">
        <p className="font-medium text-rush-dark truncate">{item.name}</p>
        <p className="text-sm text-rush-red font-semibold">
          {(item.price * item.quantity).toFixed(2)} €
        </p>
        {item.modifiers.length > 0 && (
          <p className="text-xs text-gray-400 mt-0.5 truncate">
            {item.modifiers.map((m) => m.value).join(', ')}
          </p>
        )}
        {item.notes !== undefined && (
          <p className="text-xs text-gray-400 mt-0.5 truncate italic">"{item.notes}"</p>
        )}
      </div>

      <div className="flex items-center gap-2 flex-shrink-0">
        <button
          onClick={() => updateQuantity(item.key, item.quantity - 1)}
          className="h-7 w-7 rounded-full border border-gray-200 flex items-center justify-center hover:bg-gray-100 text-lg leading-none"
          aria-label="Reducir cantidad"
        >
          −
        </button>
        <span className="w-5 text-center font-medium text-sm tabular-nums">{item.quantity}</span>
        <button
          onClick={() => updateQuantity(item.key, item.quantity + 1)}
          className="h-7 w-7 rounded-full bg-rush-red text-white flex items-center justify-center hover:bg-rush-red-hover text-lg leading-none"
          aria-label="Aumentar cantidad"
        >
          +
        </button>
        <button
          onClick={() => removeItem(item.key)}
          className="ml-1 text-gray-300 hover:text-red-500 transition-colors"
          aria-label={`Eliminar ${item.name}`}
        >
          <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path
              fillRule="evenodd"
              d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2v10a2 2 0 002 2h8a2 2 0 002-2V6a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zM7 8a1 1 0 012 0v6a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z"
              clipRule="evenodd"
            />
          </svg>
        </button>
      </div>
    </div>
  )
}
