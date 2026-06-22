import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@shared/api/axios'
import { ENDPOINTS } from '@shared/api/endpoints'
import { useCartStore } from '@shared/store/cartStore'
import { Spinner } from '@shared/components/Spinner'
import { formatCurrency, formatDate } from '@shared/utils/format'

interface HistoryOrderItem {
  productId: string
  name:      string
  quantity:  number
  unitPrice: number
}

interface HistoryOrder {
  id:             string
  number:         string
  restaurantName: string
  createdAt:      string
  totalAmount:    number
  status:         string
  items:          HistoryOrderItem[]
}

const STATUS_LABEL: Record<string, string> = {
  New: 'Nuevo', Preparing: 'En preparación', Ready: 'Listo',
  Served: 'Servido', Paid: 'Pagado', Cancelled: 'Cancelado',
}

const STATUS_COLOR: Record<string, string> = {
  Served:    'bg-green-100 text-green-700',
  Paid:      'bg-blue-100 text-blue-700',
  Cancelled: 'bg-red-100 text-red-600',
}

function StatusPill({ status }: { status: string }) {
  const cls = STATUS_COLOR[status] ?? 'bg-gray-100 text-gray-600'
  return (
    <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${cls}`}>
      {STATUS_LABEL[status] ?? status}
    </span>
  )
}

export default function OrderHistoryPage() {
  const navigate  = useNavigate()
  const addItem   = useCartStore((s) => s.addItem)
  const [expanded,  setExpanded]  = useState<string | null>(null)
  const [repeating, setRepeating] = useState<string | null>(null)

  const { data: orders, isLoading } = useQuery<HistoryOrder[]>({
    queryKey: ['order-history'],
    queryFn:  async () => {
      const { data } = await apiClient.get<HistoryOrder[]>(ENDPOINTS.profile.history)
      return data
    },
    staleTime: 2 * 60 * 1000,
  })

  const handleRepeat = (order: HistoryOrder) => {
    if (repeating !== null) return
    setRepeating(order.id)
    order.items.forEach((item) => {
      addItem({
        productId: item.productId,
        name:      item.name,
        price:     item.unitPrice,
        quantity:  item.quantity,
        modifiers: [],
      })
    })
    setTimeout(() => {
      setRepeating(null)
      navigate('/')
    }, 700)
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="sticky top-0 z-20 bg-white border-b px-4 py-3 flex items-center gap-3 shadow-sm">
        <Link to="/profile" aria-label="Volver">
          <svg className="h-6 w-6 text-gray-600" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
        </Link>
        <h1 className="text-base font-bold text-rush-dark">Historial de pedidos</h1>
      </header>

      <main className="max-w-md mx-auto px-4 py-4 space-y-3">
        {isLoading && (
          <div className="flex justify-center py-12"><Spinner size="lg" /></div>
        )}

        {!isLoading && (orders ?? []).length === 0 && (
          <div className="text-center py-16 space-y-3">
            <p className="text-4xl">🧾</p>
            <p className="font-semibold text-rush-dark">Sin pedidos aún</p>
            <p className="text-sm text-gray-400">Tus pedidos aparecerán aquí.</p>
          </div>
        )}

        {(orders ?? []).map((order) => (
          <div key={order.id} className="rounded-2xl bg-white border border-gray-100 overflow-hidden">
            {/* Collapsible header */}
            <button
              type="button"
              className="w-full text-left px-4 py-3"
              onClick={() => setExpanded(expanded === order.id ? null : order.id)}
            >
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-bold text-rush-dark text-sm">{order.number}</span>
                    <StatusPill status={order.status} />
                  </div>
                  <p className="text-xs text-gray-500 mt-0.5">{order.restaurantName}</p>
                  <p className="text-xs text-gray-400">
                    {formatDate(order.createdAt, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })}
                  </p>
                </div>
                <div className="flex flex-col items-end gap-1 flex-shrink-0">
                  <span className="font-bold text-rush-red">{formatCurrency(order.totalAmount)}</span>
                  <svg
                    className={`h-4 w-4 text-gray-400 transition-transform ${expanded === order.id ? 'rotate-180' : ''}`}
                    viewBox="0 0 20 20" fill="currentColor"
                  >
                    <path fillRule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clipRule="evenodd" />
                  </svg>
                </div>
              </div>
              {expanded !== order.id && (
                <p className="text-xs text-gray-400 mt-1.5 truncate">
                  {order.items.map((i) => `${i.quantity}× ${i.name}`).join(', ')}
                </p>
              )}
            </button>

            {/* Expanded detail */}
            {expanded === order.id && (
              <div className="border-t border-gray-50 px-4 py-3 space-y-2">
                {order.items.map((item, idx) => (
                  <div key={`${item.productId}-${idx}`} className="flex justify-between text-sm">
                    <span className="text-gray-700">{item.quantity}× {item.name}</span>
                    <span className="text-gray-500 tabular-nums">
                      {formatCurrency(item.unitPrice * item.quantity)}
                    </span>
                  </div>
                ))}
                <div className="flex items-center gap-2 pt-2">
                  <button
                    type="button"
                    onClick={() => handleRepeat(order)}
                    disabled={repeating !== null}
                    className="flex-1 rounded-xl bg-rush-red/10 text-rush-red font-semibold text-sm py-2.5 hover:bg-rush-red/20 transition-colors disabled:opacity-50"
                  >
                    {repeating === order.id ? '✓ Añadido' : '↺ Repetir pedido'}
                  </button>
                  <Link
                    to={`/tracking/${order.id}`}
                    className="flex-1 rounded-xl border border-gray-200 text-gray-600 font-medium text-sm py-2.5 text-center hover:border-gray-300 transition-colors"
                  >
                    Ver detalle
                  </Link>
                </div>
              </div>
            )}
          </div>
        ))}
      </main>
    </div>
  )
}
