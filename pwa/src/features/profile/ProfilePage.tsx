import { useState, useRef, type ChangeEvent, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@shared/api/axios'
import { useAuth } from '@shared/hooks/useAuth'
import { useAuthStore } from '@shared/store/authStore'
import { ENDPOINTS } from '@shared/api/endpoints'
import { Spinner } from '@shared/components/Spinner'
import { AuthSheet } from '@features/auth/AuthSheet'
import type { SavedPaymentMethod } from '@shared/types'

// ── Dietary preference options (matches menu filter keys) ────────
const DIETARY_OPTIONS = [
  { key: 'vegetarian', label: 'Vegetariano',   icon: '🥦' },
  { key: 'vegan',      label: 'Vegano',         icon: '🌿' },
  { key: 'glutenFree', label: 'Sin gluten',     icon: '🌾' },
  { key: 'dairyFree',  label: 'Sin lactosa',    icon: '🥛' },
  { key: 'spicy',      label: 'Me gusta picante', icon: '🌶️' },
] as const

// ── Section wrapper ──────────────────────────────────────────────
function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-2xl bg-white border border-gray-100 overflow-hidden">
      <div className="px-4 py-3 border-b border-gray-50">
        <h2 className="font-semibold text-sm text-gray-500 uppercase tracking-wide">{title}</h2>
      </div>
      {children}
    </section>
  )
}

// ── Row ──────────────────────────────────────────────────────────
function Row({ label, value, onClick }: { label: string; value?: string; onClick?: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="w-full flex items-center justify-between px-4 py-3 hover:bg-gray-50 transition-colors text-left"
    >
      <span className="text-sm text-gray-700">{label}</span>
      <div className="flex items-center gap-2">
        {value !== undefined && <span className="text-sm text-gray-400">{value}</span>}
        <svg className="h-4 w-4 text-gray-300" viewBox="0 0 20 20" fill="currentColor">
          <path fillRule="evenodd" d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z" clipRule="evenodd" />
        </svg>
      </div>
    </button>
  )
}

// ── Edit field sheet ─────────────────────────────────────────────
function EditFieldSheet({
  field, label, value, type = 'text',
  onSave, onClose,
}: {
  field:   string
  label:   string
  value:   string
  type?:   string
  onSave:  (value: string) => Promise<void>
  onClose: () => void
}) {
  const [val,     setVal]     = useState(value)
  const [loading, setLoading] = useState(false)
  void field

  const handleSave = async () => {
    setLoading(true)
    try {
      await onSave(val)
      onClose()
    } finally {
      setLoading(false)
    }
  }

  return (
    <>
      <div className="fixed inset-0 z-50 bg-black/40" onClick={onClose} aria-hidden />
      <div className="fixed inset-x-0 bottom-0 z-50 rounded-t-3xl bg-white px-5 pt-4 pb-10 shadow-2xl">
        <div className="flex justify-center mb-4">
          <div className="h-1 w-10 rounded-full bg-gray-200" aria-hidden />
        </div>
        <h3 className="font-bold text-rush-dark mb-4">{label}</h3>
        <input
          type={type}
          value={val}
          onChange={(e) => setVal(e.target.value)}
          className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm focus:outline-none focus:ring-2 focus:ring-rush-red mb-4"
          autoFocus
        />
        <div className="flex gap-3">
          <button
            type="button"
            onClick={onClose}
            className="flex-1 rounded-2xl border border-gray-200 py-3 text-sm font-medium text-gray-600"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={loading}
            className="flex-[2] rounded-2xl bg-rush-red py-3 text-sm font-bold text-white disabled:opacity-50 flex items-center justify-center"
          >
            {loading
              ? <div className="h-4 w-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
              : 'Guardar'}
          </button>
        </div>
      </div>
    </>
  )
}

// ── Avatar upload ────────────────────────────────────────────────
function AvatarSection() {
  const user       = useAuthStore((s) => s.user)
  const updateUser = useAuthStore((s) => s.updateUser)
  const fileRef    = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)

  const handleFile = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file === undefined) return
    setUploading(true)
    try {
      const form = new FormData()
      form.append('avatar', file)
      const { data } = await apiClient.post<{ avatarUrl: string }>(ENDPOINTS.profile.avatar, form)
      updateUser({ avatarUrl: data.avatarUrl })
    } catch {
      // silently fail
    } finally {
      setUploading(false)
    }
  }

  const initials = user?.fullName.trim().charAt(0).toUpperCase() ?? '?'

  return (
    <div className="flex flex-col items-center py-6 gap-3">
      <button
        type="button"
        onClick={() => fileRef.current?.click()}
        className="relative"
        disabled={uploading}
        aria-label="Cambiar foto de perfil"
      >
        {user?.avatarUrl !== undefined ? (
          <img
            src={user.avatarUrl}
            alt={user.fullName}
            className="h-20 w-20 rounded-full object-cover ring-2 ring-rush-red/20"
          />
        ) : (
          <div className="h-20 w-20 rounded-full bg-rush-red flex items-center justify-center text-white text-3xl font-bold ring-2 ring-rush-red/20">
            {initials}
          </div>
        )}
        <div className="absolute bottom-0 right-0 h-7 w-7 rounded-full bg-rush-red text-white flex items-center justify-center shadow-md">
          {uploading
            ? <div className="h-3 w-3 border border-white border-t-transparent rounded-full animate-spin" />
            : <svg className="h-3.5 w-3.5" viewBox="0 0 20 20" fill="currentColor"><path d="M13.586 3.586a2 2 0 112.828 2.828l-.793.793-2.828-2.828.793-.793zM11.379 5.793L3 14.172V17h2.828l8.38-8.379-2.83-2.828z" /></svg>
          }
        </div>
      </button>
      <input ref={fileRef} type="file" accept="image/*" className="hidden" onChange={handleFile} />
      <div className="text-center">
        <p className="font-bold text-rush-dark text-lg">{user?.fullName ?? 'Invitado'}</p>
        {user?.email !== undefined && (
          <p className="text-sm text-gray-400">{user.email}</p>
        )}
      </div>
    </div>
  )
}

// ── Dietary preferences ───────────────────────────────────────────
function DietaryPrefsSection() {
  const user       = useAuthStore((s) => s.user)
  const { updateProfile } = useAuth()
  const [saving, setSaving] = useState(false)

  const prefs = user?.dietaryPrefs ?? []

  const toggle = async (key: string) => {
    const next = prefs.includes(key)
      ? prefs.filter((p) => p !== key)
      : [...prefs, key]
    setSaving(true)
    try {
      await updateProfile({ dietaryPrefs: next })
    } finally {
      setSaving(false)
    }
  }

  return (
    <Section title="Preferencias alimentarias">
      <p className="text-xs text-gray-400 px-4 pt-2 pb-1">
        Se aplican como filtros al abrir cualquier menú.
      </p>
      {DIETARY_OPTIONS.map((opt) => (
        <button
          key={opt.key}
          type="button"
          onClick={() => toggle(opt.key)}
          disabled={saving}
          className="w-full flex items-center justify-between px-4 py-3 hover:bg-gray-50 transition-colors"
        >
          <div className="flex items-center gap-3">
            <span className="text-xl">{opt.icon}</span>
            <span className="text-sm text-gray-700">{opt.label}</span>
          </div>
          <div className={[
            'h-5 w-9 rounded-full transition-colors relative',
            prefs.includes(opt.key) ? 'bg-rush-red' : 'bg-gray-200',
          ].join(' ')}>
            <div className={[
              'absolute top-0.5 h-4 w-4 rounded-full bg-white shadow transition-transform',
              prefs.includes(opt.key) ? 'translate-x-4' : 'translate-x-0.5',
            ].join(' ')} />
          </div>
        </button>
      ))}
    </Section>
  )
}

// ── Saved payment methods ────────────────────────────────────────
function SavedPaymentMethods() {
  const qc = useQueryClient()

  const { data: methods, isLoading } = useQuery<SavedPaymentMethod[]>({
    queryKey: ['payment-methods'],
    queryFn: async () => {
      const { data } = await apiClient.get<SavedPaymentMethod[]>(ENDPOINTS.profile.paymentMethods)
      return data
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => apiClient.delete(ENDPOINTS.profile.deletePaymentMethod(id)),
    onSuccess:  () => void qc.invalidateQueries({ queryKey: ['payment-methods'] }),
  })

  const brandIcon = (brand: string) => {
    const icons: Record<string, string> = { visa: '💳', mastercard: '💳', amex: '💳' }
    return icons[brand.toLowerCase()] ?? '💳'
  }

  return (
    <Section title="Métodos de pago guardados">
      {isLoading && <div className="flex justify-center py-4"><Spinner /></div>}
      {!isLoading && (methods ?? []).length === 0 && (
        <p className="text-sm text-gray-400 px-4 py-3">Sin métodos guardados.</p>
      )}
      {(methods ?? []).map((m) => (
        <div key={m.id} className="flex items-center gap-3 px-4 py-3">
          <span className="text-xl">{brandIcon(m.brand)}</span>
          <div className="flex-1">
            <p className="text-sm font-medium text-rush-dark capitalize">
              {m.brand} •••• {m.last4}
              {m.isDefault && <span className="ml-2 text-xs text-rush-red font-semibold">Principal</span>}
            </p>
            <p className="text-xs text-gray-400">Vence {m.expMonth}/{m.expYear}</p>
          </div>
          <button
            type="button"
            onClick={() => deleteMutation.mutate(m.id)}
            className="text-xs text-red-400 hover:text-red-600"
            aria-label="Eliminar"
          >
            Eliminar
          </button>
        </div>
      ))}
    </Section>
  )
}

// ── Notification settings ────────────────────────────────────────
function NotificationSettings() {
  const user = useAuthStore((s) => s.user)
  const { updateProfile } = useAuth()
  const [saving, setSaving] = useState(false)

  const toggle = async (key: 'notifyOrderReady' | 'notifyPromotions') => {
    const next = !(user?.[key] ?? true)
    setSaving(true)
    try {
      await updateProfile({ [key]: next })
    } finally {
      setSaving(false)
    }
  }

  const items = [
    { key: 'notifyOrderReady' as const, label: 'Pedido listo', desc: 'Cuando tu pedido esté en la mesa' },
    { key: 'notifyPromotions' as const, label: 'Ofertas y promociones', desc: 'Descuentos y novedades' },
  ]

  return (
    <Section title="Notificaciones">
      {items.map((item) => (
        <button
          key={item.key}
          type="button"
          onClick={() => toggle(item.key)}
          disabled={saving}
          className="w-full flex items-center justify-between px-4 py-3 hover:bg-gray-50 transition-colors"
        >
          <div className="text-left">
            <p className="text-sm font-medium text-rush-dark">{item.label}</p>
            <p className="text-xs text-gray-400">{item.desc}</p>
          </div>
          <div className={[
            'h-5 w-9 rounded-full transition-colors relative flex-shrink-0',
            (user?.[item.key] ?? true) ? 'bg-rush-red' : 'bg-gray-200',
          ].join(' ')}>
            <div className={[
              'absolute top-0.5 h-4 w-4 rounded-full bg-white shadow transition-transform',
              (user?.[item.key] ?? true) ? 'translate-x-4' : 'translate-x-0.5',
            ].join(' ')} />
          </div>
        </button>
      ))}
    </Section>
  )
}

// ── Main ProfilePage ─────────────────────────────────────────────
export default function ProfilePage() {
  const navigate = useNavigate()
  const { isAuthenticated, updateProfile, logout } = useAuth()
  const user = useAuthStore((s) => s.user)
  const [authOpen, setAuthOpen]     = useState(false)
  const [editField, setEditField]   = useState<{ field: string; label: string; value: string; type?: string } | null>(null)

  const handleLogout = () => {
    logout()
    navigate('/')
  }

  const handleSaveField = async (value: string) => {
    if (editField === null) return
    const key = editField.field as 'fullName' | 'phone'
    await updateProfile({ [key]: value })
  }

  if (!isAuthenticated) {
    return (
      <div className="min-h-screen bg-gray-50 flex flex-col items-center justify-center px-6 text-center gap-5">
        <div className="h-20 w-20 rounded-full bg-gray-100 flex items-center justify-center text-4xl">👤</div>
        <h1 className="text-xl font-black text-rush-dark">Mi perfil</h1>
        <p className="text-gray-500 text-sm max-w-xs">
          Inicia sesión para ver tu perfil, historial de pedidos y gestionar tu cuenta.
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

  return (
    <div className="min-h-screen bg-gray-50 pb-10">
      {/* Avatar + name */}
      <AvatarSection />

      <div className="max-w-md mx-auto px-4 space-y-4">
        {/* Personal info */}
        <Section title="Información personal">
          <Row
            label="Nombre"
            value={user?.fullName}
            onClick={() => setEditField({ field: 'fullName', label: 'Tu nombre', value: user?.fullName ?? '' })}
          />
          <Row
            label="Email"
            value={user?.email}
          />
          <Row
            label="Teléfono"
            value={user?.phone ?? 'Añadir'}
            onClick={() => setEditField({ field: 'phone', label: 'Teléfono', value: user?.phone ?? '', type: 'tel' })}
          />
        </Section>

        {/* Loyalty */}
        <Section title="Fidelización">
          <Link
            to="/loyalty"
            className="flex items-center justify-between px-4 py-3 hover:bg-gray-50 transition-colors"
          >
            <div className="flex items-center gap-3">
              <span className="text-xl">⭐</span>
              <div>
                <p className="text-sm font-medium text-rush-dark">Mis puntos</p>
                <p className="text-xs text-gray-400">
                  {user?.loyaltyPoints.toLocaleString('es') ?? 0} pts · {user?.loyaltyLevel ?? 'Bronze'}
                </p>
              </div>
            </div>
            <svg className="h-4 w-4 text-gray-300" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z" clipRule="evenodd" />
            </svg>
          </Link>
        </Section>

        {/* Order history */}
        <Section title="Pedidos">
          <Link
            to="/profile/orders"
            className="flex items-center justify-between px-4 py-3 hover:bg-gray-50 transition-colors"
          >
            <div className="flex items-center gap-3">
              <span className="text-xl">🧾</span>
              <p className="text-sm font-medium text-rush-dark">Historial de pedidos</p>
            </div>
            <svg className="h-4 w-4 text-gray-300" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z" clipRule="evenodd" />
            </svg>
          </Link>
        </Section>

        {/* Dietary prefs */}
        <DietaryPrefsSection />

        {/* Notifications */}
        <NotificationSettings />

        {/* Saved payments */}
        <SavedPaymentMethods />

        {/* Logout */}
        <button
          type="button"
          onClick={handleLogout}
          className="w-full rounded-2xl border border-red-200 text-rush-red font-semibold py-3.5 hover:bg-red-50 transition-colors text-sm"
        >
          Cerrar sesión
        </button>
      </div>

      {/* Edit field sheet */}
      {editField !== null && (
        <EditFieldSheet
          {...editField}
          onSave={handleSaveField}
          onClose={() => setEditField(null)}
        />
      )}
    </div>
  )
}
