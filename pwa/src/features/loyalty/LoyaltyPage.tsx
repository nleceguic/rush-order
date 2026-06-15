import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useGet } from '@shared/hooks/useApi'
import { useAuth } from '@shared/hooks/useAuth'
import { ENDPOINTS } from '@shared/api/endpoints'
import { Spinner } from '@shared/components/Spinner'
import { AuthSheet } from '@features/auth/AuthSheet'
import type { LoyaltyLevel } from '@shared/types'

// ── Types ────────────────────────────────────────────────────────
interface PointHistoryEntry {
  id:          string
  date:        string
  description: string
  points:      number
}

interface LoyaltyDashboard {
  level:           LoyaltyLevel
  points:          number
  pointsToNext:    number | null
  progressPercent: number
  currentBenefits: string[]
  nextBenefits:    string[]
  history:         PointHistoryEntry[]
}

// ── Level config ─────────────────────────────────────────────────
const LEVEL_CONFIG: Record<LoyaltyLevel, {
  icon:     string
  label:    string
  gradient: string
  ring:     string
  text:     string
}> = {
  Bronze:   { icon: '🥉', label: 'Bronce',   gradient: 'from-amber-700 to-amber-500',    ring: 'ring-amber-300',  text: 'text-amber-800' },
  Silver:   { icon: '🥈', label: 'Plata',    gradient: 'from-slate-500 to-slate-400',     ring: 'ring-slate-300',  text: 'text-slate-700' },
  Gold:     { icon: '🥇', label: 'Oro',      gradient: 'from-yellow-500 to-yellow-400',   ring: 'ring-yellow-300', text: 'text-yellow-700' },
  Platinum: { icon: '💎', label: 'Platino',  gradient: 'from-purple-700 to-purple-500',   ring: 'ring-purple-300', text: 'text-purple-800' },
}

// ── Animated counter ─────────────────────────────────────────────
function AnimatedCounter({ target }: { target: number }) {
  const [display, setDisplay] = useState(0)
  const frameRef = useRef<number | null>(null)

  useEffect(() => {
    const duration  = 900
    const startTime = performance.now()
    const startVal  = display

    const step = (now: number) => {
      const t = Math.min((now - startTime) / duration, 1)
      const ease = 1 - Math.pow(1 - t, 3) // cubic ease-out
      setDisplay(Math.round(startVal + (target - startVal) * ease))
      if (t < 1) frameRef.current = requestAnimationFrame(step)
    }

    frameRef.current = requestAnimationFrame(step)
    return () => { if (frameRef.current !== null) cancelAnimationFrame(frameRef.current) }
  }, [target]) // eslint-disable-line react-hooks/exhaustive-deps

  return <span>{display.toLocaleString('es')}</span>
}

// ── Main page ────────────────────────────────────────────────────
export default function LoyaltyPage() {
  const { isAuthenticated } = useAuth()
  const [authOpen, setAuthOpen] = useState(false)

  const { data, isLoading } = useGet<LoyaltyDashboard>(
    ['loyalty-dashboard'],
    ENDPOINTS.loyalty.profile,
    { enabled: isAuthenticated },
  )

  if (!isAuthenticated) {
    return (
      <div className="min-h-screen bg-gray-50 flex flex-col items-center justify-center px-6 text-center gap-5">
        <div className="text-5xl">⭐</div>
        <h1 className="text-xl font-black text-rush-dark">Programa de fidelización</h1>
        <p className="text-gray-500 text-sm max-w-xs">
          Acumula puntos en cada pedido y canjéalos por descuentos. Inicia sesión para ver tu saldo.
        </p>
        <button
          onClick={() => setAuthOpen(true)}
          className="px-6 py-3 bg-rush-red text-white font-bold rounded-2xl hover:bg-rush-red-hover transition-colors"
        >
          Entrar / Registrarse
        </button>
        <AuthSheet open={authOpen} onClose={() => setAuthOpen(false)} />
      </div>
    )
  }

  if (isLoading) {
    return <div className="min-h-screen flex items-center justify-center"><Spinner size="lg" /></div>
  }

  if (data === undefined) {
    return (
      <div className="min-h-screen flex items-center justify-center text-sm text-gray-400">
        Sin datos de fidelización aún.
      </div>
    )
  }

  const cfg = LEVEL_CONFIG[data.level]
  const nextLevel = data.pointsToNext !== null
    ? `${data.pointsToNext.toLocaleString('es')} puntos para ${nextLevelLabel(data.level)}`
    : '¡Nivel máximo alcanzado!'

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="sticky top-0 z-20 bg-white border-b px-4 py-3 flex items-center gap-3 shadow-sm">
        <Link to="/profile" aria-label="Volver">
          <svg className="h-6 w-6 text-gray-600" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
        </Link>
        <h1 className="text-base font-bold text-rush-dark">Mis puntos</h1>
      </header>

      <main className="max-w-md mx-auto px-4 py-6 space-y-5">
        {/* Hero card */}
        <div className={`rounded-3xl bg-gradient-to-br ${cfg.gradient} text-white p-6 shadow-xl`}>
          {/* Level badge */}
          <div className="flex items-center gap-3 mb-5">
            <div className={`h-14 w-14 rounded-full bg-white/20 ring-2 ${cfg.ring} flex items-center justify-center text-3xl`}>
              {cfg.icon}
            </div>
            <div>
              <p className="text-xs font-medium opacity-80 uppercase tracking-wider">Nivel actual</p>
              <p className="text-xl font-black">{cfg.label}</p>
            </div>
          </div>

          {/* Points */}
          <div className="text-center mb-4">
            <p className="text-6xl font-black tabular-nums">
              <AnimatedCounter target={data.points} />
            </p>
            <p className="text-sm opacity-80 mt-1">puntos disponibles</p>
          </div>

          {/* Progress bar */}
          {data.pointsToNext !== null && (
            <div className="space-y-1.5">
              <div className="h-2 rounded-full bg-white/30 overflow-hidden">
                <div
                  className="h-full bg-white rounded-full transition-all duration-1000"
                  style={{ width: `${Math.min(data.progressPercent, 100)}%` }}
                />
              </div>
              <p className="text-xs text-center opacity-75">{nextLevel}</p>
            </div>
          )}
          {data.pointsToNext === null && (
            <p className="text-sm text-center opacity-90 font-semibold">✨ {nextLevel}</p>
          )}
        </div>

        {/* Benefits */}
        <section className="rounded-2xl bg-white border border-gray-100 divide-y overflow-hidden">
          <div className="px-4 py-3">
            <h2 className="font-semibold text-rush-dark text-sm">Beneficios de tu nivel</h2>
          </div>
          {data.currentBenefits.map((b, i) => (
            <div key={i} className="flex items-center gap-3 px-4 py-2.5">
              <span className="text-green-500 flex-shrink-0">✓</span>
              <span className="text-sm text-gray-700">{b}</span>
            </div>
          ))}
          {data.nextBenefits.length > 0 && (
            <>
              <div className="px-4 py-2 bg-gray-50">
                <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
                  Próximo nivel — {nextLevelLabel(data.level)}
                </p>
              </div>
              {data.nextBenefits.map((b, i) => (
                <div key={i} className="flex items-center gap-3 px-4 py-2.5 opacity-60">
                  <span className="text-gray-300 flex-shrink-0">◦</span>
                  <span className="text-sm text-gray-500">{b}</span>
                </div>
              ))}
            </>
          )}
        </section>

        {/* History */}
        <section>
          <h2 className="font-semibold text-rush-dark mb-3">Historial de puntos</h2>
          <div className="rounded-2xl bg-white border border-gray-100 divide-y overflow-hidden">
            {data.history.length === 0 && (
              <p className="text-center text-gray-400 py-8 text-sm">Sin movimientos aún</p>
            )}
            {data.history.map((entry) => (
              <div key={entry.id} className="flex items-center gap-3 px-4 py-3">
                <div
                  className={[
                    'h-8 w-8 rounded-full flex items-center justify-center text-sm font-bold flex-shrink-0',
                    entry.points > 0 ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-600',
                  ].join(' ')}
                >
                  {entry.points > 0 ? '+' : '−'}
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-rush-dark truncate">{entry.description}</p>
                  <p className="text-xs text-gray-400">
                    {new Date(entry.date).toLocaleDateString('es', { day: 'numeric', month: 'short', year: 'numeric' })}
                  </p>
                </div>
                <span className={[
                  'font-bold text-sm tabular-nums flex-shrink-0',
                  entry.points > 0 ? 'text-green-600' : 'text-red-500',
                ].join(' ')}>
                  {entry.points > 0 ? '+' : ''}{entry.points}
                </span>
              </div>
            ))}
          </div>
        </section>
      </main>
    </div>
  )
}

function nextLevelLabel(current: LoyaltyLevel): string {
  const map: Partial<Record<LoyaltyLevel, string>> = {
    Bronze: 'Plata',
    Silver: 'Oro',
    Gold:   'Platino',
  }
  return map[current] ?? ''
}
