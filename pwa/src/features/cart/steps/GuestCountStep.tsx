interface GuestCountStepProps {
  value:    number
  onChange: (n: number) => void
  onNext:   () => void
  onBack:   () => void
}

const COUNTS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] as const

export function GuestCountStep({ value, onChange, onNext, onBack }: GuestCountStepProps) {
  return (
    <div className="flex flex-col h-full min-h-0">
      <div className="flex items-center gap-3 px-5 py-4 border-b flex-shrink-0">
        <button onClick={onBack} aria-label="Volver" className="text-gray-400 hover:text-gray-700">
          <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M12.707 5.293a1 1 0 010 1.414L9.414 10l3.293 3.293a1 1 0 01-1.414 1.414l-4-4a1 1 0 010-1.414l4-4a1 1 0 011.414 0z" clipRule="evenodd" />
          </svg>
        </button>
        <h2 className="text-lg font-bold text-rush-dark">¿Cuántos comensales?</h2>
      </div>

      <div className="flex-1 flex flex-col items-center justify-center px-5 pb-4 overflow-y-auto scrollbar-hide">
        <p className="text-sm text-gray-500 mb-8 text-center">
          Ayuda al restaurante a preparar la mesa correctamente
        </p>

        <div className="grid grid-cols-5 gap-3 w-full max-w-xs">
          {COUNTS.map((n) => (
            <button
              key={n}
              onClick={() => onChange(n)}
              className={`
                aspect-square rounded-2xl text-lg font-bold transition-all
                ${value === n
                  ? 'bg-rush-red text-white shadow-lg scale-110'
                  : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                }
              `}
              aria-label={`${n} ${n === 1 ? 'comensal' : 'comensales'}`}
              aria-pressed={value === n}
            >
              {n}
            </button>
          ))}
        </div>

        <p className="mt-6 text-sm text-gray-500">
          {value} {value === 1 ? 'comensal' : 'comensales'}
        </p>
      </div>

      <div className="flex-shrink-0 border-t bg-white px-5 py-4">
        <button
          onClick={onNext}
          className="w-full rounded-2xl bg-rush-red py-4 font-bold text-white text-base hover:bg-rush-red-hover transition-colors"
        >
          Continuar
        </button>
      </div>
    </div>
  )
}
