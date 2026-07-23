import { create } from 'zustand'
import { useEffect } from 'react'
import { generateUUID } from '../utils/deviceFingerprint'

type ToastType = 'success' | 'error' | 'info' | 'warning'

interface ToastItem {
  id:       string
  message:  string
  type:     ToastType
  duration: number
}

interface ToastStore {
  toasts: ToastItem[]
  push:   (message: string, type?: ToastType, duration?: number) => void
  remove: (id: string) => void
}

const useToastStore = create<ToastStore>()((set) => ({
  toasts: [],
  push:   (message, type = 'info', duration = 3500) =>
    set((s) => ({
      toasts: [...s.toasts, { id: generateUUID(), message, type, duration }],
    })),
  remove: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}))

export const toast = {
  success: (msg: string) => useToastStore.getState().push(msg, 'success'),
  error:   (msg: string) => useToastStore.getState().push(msg, 'error'),
  info:    (msg: string) => useToastStore.getState().push(msg, 'info'),
  warning: (msg: string) => useToastStore.getState().push(msg, 'warning'),
}

const TYPE_CLASSES: Record<ToastType, string> = {
  success: 'bg-green-600 text-white',
  error:   'bg-red-600 text-white',
  info:    'bg-rush-dark text-white',
  warning: 'bg-yellow-500 text-white',
}

function ToastEntry({ item, onRemove }: { item: ToastItem; onRemove: () => void }) {
  useEffect(() => {
    const timer = setTimeout(onRemove, item.duration)
    return () => clearTimeout(timer)
  }, [item.duration, onRemove])

  return (
    <div
      className={`flex items-center gap-3 rounded-xl px-4 py-3 shadow-lg text-sm font-medium ${TYPE_CLASSES[item.type]}`}
    >
      <span className="flex-1">{item.message}</span>
      <button onClick={onRemove} className="opacity-70 hover:opacity-100 transition-opacity" aria-label="Cerrar">
        ✕
      </button>
    </div>
  )
}

export function ToastContainer() {
  const { toasts, remove } = useToastStore()

  return (
    <div className="fixed bottom-4 left-1/2 -translate-x-1/2 z-50 flex flex-col gap-2 w-full max-w-sm px-4 pointer-events-none">
      {toasts.map((t) => (
        <div key={t.id} className="pointer-events-auto">
          <ToastEntry item={t} onRemove={() => remove(t.id)} />
        </div>
      ))}
    </div>
  )
}
