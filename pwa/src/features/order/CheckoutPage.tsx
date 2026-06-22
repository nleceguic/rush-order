import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useCartStore } from '@shared/store/cartStore'
import { usePost } from '@shared/hooks/useApi'
import { ENDPOINTS } from '@shared/api/endpoints'
import { Button } from '@shared/components/Button'
import { toast } from '@shared/components/Toast'
import { formatCurrency } from '@shared/utils/format'

interface OrderItemPayload {
  productId: string
  quantity:  number
  notes?:    string
}

interface CreateOrderPayload {
  tableId: string
  items:   OrderItemPayload[]
}

interface CreateOrderResponse {
  orderId: string
}

export default function CheckoutPage() {
  const navigate = useNavigate()
  const { items, tableId, clear, total } = useCartStore()
  const [notes, setNotes] = useState('')

  const { mutate: createOrder, isPending } = usePost<CreateOrderResponse, CreateOrderPayload>(
    ENDPOINTS.orders.base,
    {
      onSuccess: ({ orderId }) => {
        clear()
        navigate(`/payment/${orderId}`)
      },
      onError: () => toast.error('No se pudo crear el pedido. Intenta de nuevo.'),
    },
  )

  const handleSubmit = () => {
    if (tableId === null) {
      toast.error('Mesa no identificada. Escanea el QR de tu mesa.')
      return
    }
    createOrder({
      tableId,
      items: items.map((i) => {
        const base: OrderItemPayload = { productId: i.productId, quantity: i.quantity }
        return i.notes !== undefined ? { ...base, notes: i.notes } : base
      }),
    })
  }

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <header className="sticky top-0 z-30 bg-white border-b px-4 py-3 flex items-center gap-3 shadow-sm">
        <button onClick={() => navigate(-1)} aria-label="Volver">
          <svg className="h-6 w-6" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
        </button>
        <h1 className="text-lg font-bold">Confirmar pedido</h1>
      </header>

      <main className="flex-1 px-4 py-4 space-y-4">
        {/* Summary */}
        <div className="rounded-xl bg-white shadow-sm p-4">
          <h2 className="font-semibold mb-3">Resumen</h2>
          {items.map((i) => (
            <div key={i.key} className="flex justify-between text-sm py-1">
              <span className="text-gray-600">{i.quantity}× {i.name}</span>
              <span className="font-medium">{formatCurrency(i.price * i.quantity)}</span>
            </div>
          ))}
          <div className="border-t mt-3 pt-3 flex justify-between font-semibold">
            <span>Total</span>
            <span className="text-rush-red">{formatCurrency(total())}</span>
          </div>
        </div>

        {/* Notes */}
        <div className="rounded-xl bg-white shadow-sm p-4">
          <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor="order-notes">
            Notas adicionales <span className="text-gray-400 font-normal">(opcional)</span>
          </label>
          <textarea
            id="order-notes"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            className="w-full rounded-lg border-gray-200 text-sm resize-none"
            placeholder="Alergias, preferencias de cocción…"
          />
        </div>
      </main>

      <div className="sticky bottom-0 bg-white border-t p-4">
        <Button className="w-full" size="lg" loading={isPending} onClick={handleSubmit}>
          Enviar pedido
        </Button>
      </div>
    </div>
  )
}
