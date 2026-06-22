import { useState, type FormEvent } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { loadStripe } from '@stripe/stripe-js'
import {
  Elements,
  PaymentElement,
  useStripe,
  useElements,
} from '@stripe/react-stripe-js'
import type { StripeElementsOptions } from '@stripe/stripe-js'
import { Spinner } from '@shared/components/Spinner'
import { TipSelector } from './components/TipSelector'
import { SplitBillSheet } from './components/SplitBillSheet'
import { usePaymentIntent } from './hooks/usePaymentIntent'
import { usePaymentEvents } from './hooks/usePaymentEvents'
import { formatCurrency } from '@shared/utils/format'

const stripeKey = String(import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY ?? '')
const stripePromise = stripeKey.length > 0 ? loadStripe(stripeKey) : Promise.resolve(null)

const APPEARANCE: StripeElementsOptions['appearance'] = {
  theme: 'flat',
  variables: {
    colorPrimary:    '#E63946',
    colorBackground: '#ffffff',
    colorText:       '#1D3557',
    colorDanger:     '#df1b41',
    fontFamily:      'Inter, system-ui, sans-serif',
    spacingUnit:     '4px',
    borderRadius:    '16px',
  },
}

// ──────────────────────────────────────────────────────────────
// Outer page — manages PI state and passes clientSecret to Elements
// ──────────────────────────────────────────────────────────────
export default function PaymentPage() {
  const { orderId = '' } = useParams<{ orderId: string }>()
  const [tipCents,       setTipCents]       = useState(0)
  const [splits,         setSplits]         = useState(1)
  const [splitOpen,      setSplitOpen]      = useState(false)

  const { data: pi, isLoading, error, updateIntent } = usePaymentIntent(orderId)
  const paidCount = usePaymentEvents(orderId, splits > 1)

  const handleTipConfirmed = async (cents: number) => {
    setTipCents(cents)
    await updateIntent(cents, splits)
  }

  const handleSplitsApplied = async (n: number) => {
    setSplits(n)
    await updateIntent(tipCents, n)
    setSplitOpen(false)
  }

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Spinner size="lg" />
      </div>
    )
  }

  if (error !== null || pi === null) {
    return (
      <div className="min-h-screen flex items-center justify-center px-4">
        <div className="text-center">
          <p className="text-rush-red font-semibold mb-2">Error al cargar el pago</p>
          <p className="text-sm text-gray-500">{error ?? 'Inténtalo de nuevo.'}</p>
        </div>
      </div>
    )
  }

  const totalWithTip     = pi.amountCents + tipCents
  const perPersonCents   = splits > 1 ? Math.ceil(totalWithTip / splits) : totalWithTip

  return (
    <>
      {/* Header */}
      <header className="sticky top-0 z-30 bg-white border-b px-4 py-3 flex items-center gap-3 shadow-sm">
        <button onClick={() => window.history.back()} aria-label="Volver">
          <svg className="h-6 w-6 text-gray-600" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
        </button>
        <h1 className="text-lg font-bold text-rush-dark">Pago digital</h1>
      </header>

      <main className="max-w-md mx-auto px-4 py-6 space-y-5">
        {/* Order summary pill */}
        <div className="flex items-center justify-between rounded-2xl bg-gray-50 border px-4 py-3">
          <div>
            <p className="text-xs text-gray-400">Pedido</p>
            <p className="font-semibold text-rush-dark text-sm">#{orderId.slice(0, 8).toUpperCase()}</p>
          </div>
          <div className="text-right">
            <p className="text-xs text-gray-400">Total</p>
            <p className="font-bold text-rush-red">{formatCurrency(totalWithTip / 100)}</p>
          </div>
          {splits > 1 && (
            <div className="text-right">
              <p className="text-xs text-gray-400">Tu parte ({splits} personas)</p>
              <p className="font-bold text-rush-dark">{formatCurrency(perPersonCents / 100)}</p>
            </div>
          )}
        </div>

        {/* Tip selector */}
        <TipSelector
          baseAmountCents={pi.amountCents}
          onConfirm={handleTipConfirmed}
        />

        {/* Split bill */}
        <button
          type="button"
          onClick={() => setSplitOpen(true)}
          className="w-full flex items-center justify-between rounded-2xl border border-gray-200 px-4 py-3 text-sm hover:border-rush-red hover:text-rush-red transition-colors"
        >
          <span className="flex items-center gap-2 font-medium text-gray-700">
            <span>💳</span>
            {splits > 1
              ? `Dividida: ${splits} personas · ${formatCurrency(perPersonCents / 100)} c/u`
              : 'Dividir la cuenta'}
          </span>
          <svg className="h-4 w-4 text-gray-400" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z" clipRule="evenodd" />
          </svg>
        </button>

        {/* Stripe Elements — remounts when PI changes */}
        <Elements
          key={pi.clientSecret}
          stripe={stripePromise}
          options={{ clientSecret: pi.clientSecret, appearance: APPEARANCE, locale: 'es' }}
        >
          <PaymentForm
            orderId={orderId}
            amountCents={perPersonCents}
          />
        </Elements>
      </main>

      <SplitBillSheet
        open={splitOpen}
        onClose={() => setSplitOpen(false)}
        totalCents={totalWithTip}
        splits={splits}
        paidCount={paidCount}
        onApply={handleSplitsApplied}
      />
    </>
  )
}

// ──────────────────────────────────────────────────────────────
// Inner form — uses Stripe hooks (must be inside <Elements>)
// ──────────────────────────────────────────────────────────────
interface PaymentFormProps {
  orderId:     string
  amountCents: number
}

function PaymentForm({ orderId, amountCents }: PaymentFormProps) {
  const stripe   = useStripe()
  const elements = useElements()
  const navigate = useNavigate()
  const [submitting, setSubmitting] = useState(false)
  const [error,      setError]      = useState<string | null>(null)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (stripe === null || elements === null || submitting) return

    setSubmitting(true)
    setError(null)

    const { error: stripeError } = await stripe.confirmPayment({
      elements,
      confirmParams: {
        return_url: `${window.location.origin}/payment/result?orderId=${orderId}`,
      },
    })

    // Only runs if redirect didn't happen (i.e., there was an error)
    if (stripeError.type === 'card_error' || stripeError.type === 'validation_error') {
      setError(stripeError.message ?? 'Error en el pago.')
    } else {
      setError('Ocurrió un error inesperado. Inténtalo de nuevo.')
    }
    setSubmitting(false)
  }

  // Fallback if Stripe key isn't configured (dev)
  const handleDevPay = () => {
    navigate(`/tracking/${orderId}`)
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {stripe !== null ? (
        <div className="rounded-2xl border border-gray-200 p-4">
          <PaymentElement options={{ layout: 'tabs' }} />
        </div>
      ) : (
        <div className="rounded-2xl border-2 border-dashed border-gray-200 p-6 text-center">
          <p className="text-sm text-gray-400 mb-3">
            Stripe no configurado — modo desarrollo
          </p>
          <button
            type="button"
            onClick={handleDevPay}
            className="px-4 py-2 bg-gray-100 rounded-xl text-sm text-gray-600 hover:bg-gray-200"
          >
            Simular pago completado
          </button>
        </div>
      )}

      {error !== null && (
        <div className="rounded-xl bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {stripe !== null && (
        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-2xl bg-rush-red py-4 font-bold text-white text-base hover:bg-rush-red-hover disabled:opacity-50 transition-colors flex items-center justify-center gap-2"
        >
          {submitting ? (
            <>
              <div className="h-4 w-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
              Procesando…
            </>
          ) : (
            `Pagar ${formatCurrency(amountCents / 100)}`
          )}
        </button>
      )}

      <p className="text-center text-[11px] text-gray-400">
        🔒 Pago seguro con cifrado SSL · Stripe
      </p>
    </form>
  )
}
