import { useState } from 'react'
import type { CompletedOrder } from '@shared/types'
import { formatCurrency } from '@shared/utils/format'

interface PastOrdersPanelProps {
  orders: CompletedOrder[]
}

export function PastOrdersPanel({ orders }: PastOrdersPanelProps) {
  const [open, setOpen] = useState(false)

  if (orders.length === 0) return null

  const totalSpent = orders.reduce((sum, o) => sum + o.total, 0)

  return (
    <div className="mx-4 mb-4 rounded-xl border border-gray-100 bg-white shadow-sm overflow-hidden">
      <button
        onClick={() => setOpen((p) => !p)}
        className="w-full flex items-center justify-between px-4 py-3 text-sm font-medium text-rush-dark"
        aria-expanded={open}
      >
        <div className="flex items-center gap-2">
          <svg className="h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
              d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          <span>Mis pedidos anteriores ({orders.length})</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-gray-400 text-xs tabular-nums">{formatCurrency(totalSpent)}</span>
          <svg
            className={`h-4 w-4 text-gray-400 transition-transform ${open ? 'rotate-180' : ''}`}
            viewBox="0 0 20 20" fill="currentColor"
          >
            <path fillRule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clipRule="evenodd" />
          </svg>
        </div>
      </button>

      {open && (
        <div className="border-t divide-y divide-gray-50">
          {orders.map((order) => (
            <div key={order.id} className="px-4 py-3">
              <div className="flex justify-between items-center mb-1.5">
                <span className="text-xs font-semibold text-rush-dark">
                  {order.number} · Ronda {order.round}
                </span>
                <span className="text-xs font-bold text-rush-red tabular-nums">
                  {formatCurrency(order.total)}
                </span>
              </div>
              <ul className="space-y-0.5">
                {order.items.map((item, i) => (
                  <li key={i} className="text-xs text-gray-500 flex justify-between gap-2">
                    <span className="truncate">{item.quantity}× {item.name}</span>
                    <span className="flex-shrink-0 tabular-nums">
                      {formatCurrency(item.price * item.quantity)}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
