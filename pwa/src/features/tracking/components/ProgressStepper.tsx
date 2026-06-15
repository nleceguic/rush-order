import type { ReactNode } from 'react'
import type { OrderStatus } from '../hooks/useOrderTracking'

interface Step {
  key:   OrderStatus
  label: string
  icon:  ReactNode
}

const STEPS: Step[] = [
  {
    key:   'New',
    label: 'Pedido recibido',
    icon:  (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" className="h-5 w-5">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
          d="M9 12l2 2 4-4M7.835 4.697a3.42 3.42 0 001.946-.806 3.42 3.42 0 014.438 0 3.42 3.42 0 001.946.806 3.42 3.42 0 013.138 3.138 3.42 3.42 0 00.806 1.946 3.42 3.42 0 010 4.438 3.42 3.42 0 00-.806 1.946 3.42 3.42 0 01-3.138 3.138 3.42 3.42 0 00-1.946.806 3.42 3.42 0 01-4.438 0 3.42 3.42 0 00-1.946-.806 3.42 3.42 0 01-3.138-3.138 3.42 3.42 0 00-.806-1.946 3.42 3.42 0 010-4.438 3.42 3.42 0 00.806-1.946 3.42 3.42 0 013.138-3.138z"
        />
      </svg>
    ),
  },
  {
    key:   'Preparing',
    label: 'En preparación',
    icon:  (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" className="h-5 w-5">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
          d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"
        />
      </svg>
    ),
  },
  {
    key:   'Ready',
    label: '¡Listo!',
    icon:  (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" className="h-5 w-5">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
          d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
        />
      </svg>
    ),
  },
  {
    key:   'Served',
    label: 'Servido',
    icon:  (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" className="h-5 w-5">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
          d="M5 3h14M5 3a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2V5a2 2 0 00-2-2M5 3l7 4 7-4"
        />
      </svg>
    ),
  },
]

const ORDER: OrderStatus[] = ['New', 'Preparing', 'Ready', 'Served']

function statusIndex(s: OrderStatus): number {
  return ORDER.indexOf(s)
}

interface Props {
  status: OrderStatus
}

export function ProgressStepper({ status }: Props) {
  const current = statusIndex(status)

  return (
    <div className="relative flex items-start justify-between">
      {/* Connecting line */}
      <div className="absolute top-5 left-0 right-0 h-0.5 bg-gray-200 z-0" aria-hidden />
      <div
        className="absolute top-5 left-0 h-0.5 bg-rush-red z-0 transition-all duration-700"
        style={{ width: `${(current / (STEPS.length - 1)) * 100}%` }}
        aria-hidden
      />

      {STEPS.map((step, idx) => {
        const done   = idx < current
        const active = idx === current
        const pending = idx > current

        return (
          <div key={step.key} className="relative z-10 flex flex-col items-center gap-1.5" style={{ width: `${100 / STEPS.length}%` }}>
            <div
              className={[
                'h-10 w-10 rounded-full flex items-center justify-center border-2 transition-all duration-500',
                done    ? 'bg-green-500 border-green-500 text-white' : '',
                active  ? 'bg-rush-red border-rush-red text-white animate-pulse' : '',
                pending ? 'bg-white border-gray-200 text-gray-300' : '',
              ].join(' ')}
            >
              {step.icon}
            </div>
            <p className={[
              'text-xs font-medium text-center leading-tight px-1',
              done    ? 'text-green-600' : '',
              active  ? 'text-rush-red'  : '',
              pending ? 'text-gray-400'  : '',
            ].join(' ')}>
              {step.label}
            </p>
          </div>
        )
      })}
    </div>
  )
}
