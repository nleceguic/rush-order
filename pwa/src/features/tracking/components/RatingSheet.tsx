import { useState } from 'react'
import { apiClient } from '@shared/api/axios'
import { ENDPOINTS } from '@shared/api/endpoints'

interface StarRatingProps {
  value:    number
  onChange: (v: number) => void
  label:    string
}

function StarRating({ value, onChange, label }: StarRatingProps) {
  const [hovered, setHovered] = useState(0)

  return (
    <div className="flex items-center justify-between">
      <span className="text-sm text-gray-700">{label}</span>
      <div className="flex gap-1" role="group" aria-label={label}>
        {[1, 2, 3, 4, 5].map((star) => (
          <button
            key={star}
            type="button"
            onClick={() => onChange(star)}
            onMouseEnter={() => setHovered(star)}
            onMouseLeave={() => setHovered(0)}
            aria-label={`${star} estrella${star !== 1 ? 's' : ''}`}
            className="text-2xl leading-none transition-transform hover:scale-110"
          >
            {(hovered || value) >= star ? '⭐' : '☆'}
          </button>
        ))}
      </div>
    </div>
  )
}

interface Props {
  orderId:  string
  onClose:  () => void
}

export function RatingSheet({ orderId, onClose }: Props) {
  const [food,    setFood]    = useState(0)
  const [speed,   setSpeed]   = useState(0)
  const [service, setService] = useState(0)
  const [comment, setComment] = useState('')
  const [sent,    setSent]    = useState(false)
  const [loading, setLoading] = useState(false)

  const canSubmit = food > 0 && speed > 0 && service > 0

  const handleSubmit = async () => {
    if (!canSubmit || loading) return
    setLoading(true)
    try {
      await apiClient.post(ENDPOINTS.tracking.rating(orderId), {
        food, speed, service,
        ...(comment.length > 0 ? { comment } : {}),
      })
      setSent(true)
      setTimeout(onClose, 2500)
    } catch {
      // submit is optional — don't block UX
      setSent(true)
      setTimeout(onClose, 2500)
    } finally {
      setLoading(false)
    }
  }

  return (
    <>
      <div className="fixed inset-0 z-40 bg-black/40" onClick={onClose} aria-hidden />
      <div
        role="dialog"
        aria-modal
        aria-label="Valorar pedido"
        className="fixed inset-x-0 bottom-0 z-50 rounded-t-3xl bg-white shadow-2xl px-5 pt-4 pb-10"
        style={{ animation: 'scale-in 0.3s ease' }}
      >
        <div className="flex justify-center mb-3">
          <div className="h-1 w-10 rounded-full bg-gray-200" aria-hidden />
        </div>

        {sent ? (
          <div
            className="text-center py-8 space-y-3"
            style={{ animation: 'scale-in 0.35s ease' }}
          >
            <div className="text-5xl">🙌</div>
            <h3 className="text-lg font-black text-rush-dark">¡Gracias por tu valoración!</h3>
            <p className="text-sm text-gray-500">Tu opinión nos ayuda a mejorar.</p>
          </div>
        ) : (
          <>
            <h3 className="text-lg font-bold text-rush-dark mb-1 text-center">¿Cómo fue tu experiencia?</h3>
            <p className="text-xs text-gray-400 text-center mb-5">Tarda solo 10 segundos</p>

            <div className="space-y-4 mb-5">
              <StarRating value={food}    onChange={setFood}    label="Comida" />
              <StarRating value={speed}   onChange={setSpeed}   label="Velocidad" />
              <StarRating value={service} onChange={setService} label="Servicio" />
            </div>

            <textarea
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              placeholder="Comentario opcional…"
              maxLength={300}
              rows={3}
              className="w-full rounded-xl border border-gray-200 p-3 text-sm resize-none focus:ring-rush-red focus:border-rush-red mb-4"
            />

            <div className="flex gap-3">
              <button
                type="button"
                onClick={onClose}
                className="flex-1 rounded-2xl border border-gray-200 py-3 text-sm font-medium text-gray-600 hover:border-gray-300 transition-colors"
              >
                Omitir
              </button>
              <button
                type="button"
                onClick={handleSubmit}
                disabled={!canSubmit || loading}
                className="flex-[2] rounded-2xl bg-rush-red py-3 text-sm font-bold text-white hover:bg-rush-red-hover disabled:opacity-50 transition-colors flex items-center justify-center gap-2"
              >
                {loading ? (
                  <div className="h-4 w-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                ) : 'Enviar valoración'}
              </button>
            </div>
          </>
        )}
      </div>
    </>
  )
}
