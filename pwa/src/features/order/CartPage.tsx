import { useNavigate } from 'react-router-dom'
import { useCartStore } from '@shared/store/cartStore'
import { CartItem } from './components/CartItem'
import { Button } from '@shared/components/Button'
import { formatCurrency } from '@shared/utils/format'

export default function CartPage() {
  const navigate = useNavigate()
  const { items, total, clear } = useCartStore()

  if (items.length === 0) {
    return (
      <div className="min-h-screen bg-gray-50 flex flex-col items-center justify-center gap-4 px-4">
        <svg className="h-16 w-16 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={1}
            d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"
          />
        </svg>
        <p className="text-gray-500 text-lg font-medium">Tu carrito está vacío</p>
        <Button onClick={() => navigate('/')}>Ver menú</Button>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <header className="sticky top-0 z-30 bg-white border-b px-4 py-3 flex items-center gap-3 shadow-sm">
        <button onClick={() => navigate(-1)} aria-label="Volver">
          <svg className="h-6 w-6" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
        </button>
        <h1 className="text-lg font-bold">Mi pedido</h1>
      </header>

      <main className="flex-1 px-4 py-4">
        <div className="rounded-xl bg-white shadow-sm divide-y">
          {items.map((item) => (
            <div key={item.key} className="px-4">
              <CartItem item={item} />
            </div>
          ))}
        </div>
        <div className="mt-3 flex justify-end">
          <button
            onClick={clear}
            className="text-sm text-gray-400 hover:text-red-500 transition-colors"
          >
            Vaciar carrito
          </button>
        </div>
      </main>

      <div className="sticky bottom-0 bg-white border-t p-4 space-y-3">
        <div className="flex justify-between text-base font-semibold">
          <span>Total</span>
          <span className="text-rush-red">{formatCurrency(total())}</span>
        </div>
        <Button className="w-full" size="lg" onClick={() => navigate('/checkout')}>
          Confirmar pedido
        </Button>
      </div>
    </div>
  )
}
