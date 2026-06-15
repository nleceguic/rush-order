type SpinnerSize = 'sm' | 'md' | 'lg'

const SIZE_MAP: Record<SpinnerSize, string> = {
  sm: 'h-4 w-4',
  md: 'h-8 w-8',
  lg: 'h-12 w-12',
}

interface SpinnerProps {
  size?:      SpinnerSize
  className?: string
}

export function Spinner({ size = 'md', className = '' }: SpinnerProps) {
  return (
    <svg
      className={`animate-spin text-rush-red ${SIZE_MAP[size]} ${className}`}
      viewBox="0 0 24 24"
      fill="none"
      role="status"
      aria-label="Cargando…"
    >
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
    </svg>
  )
}
