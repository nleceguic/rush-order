import { useRef, useState, type ReactNode } from 'react'

/** Wrap matched substring in <strong> tags */
export function highlightMatch(text: string, query: string): ReactNode {
  if (query.length === 0) return text
  const escaped = query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const regex = new RegExp(`(${escaped})`, 'gi')
  const parts = text.split(regex)
  return (
    <>
      {parts.map((part, i) =>
        regex.test(part)
          ? <strong key={i} className="text-rush-red font-semibold">{part}</strong>
          : <span key={i}>{part}</span>,
      )}
    </>
  )
}

interface SearchBarProps {
  value:    string
  onChange: (q: string) => void
}

export function SearchBar({ value, onChange }: SearchBarProps) {
  const [expanded, setExpanded] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  const expand = () => {
    setExpanded(true)
    setTimeout(() => inputRef.current?.focus(), 50)
  }

  const collapse = () => {
    setExpanded(false)
    onChange('')
  }

  if (!expanded) {
    return (
      <button onClick={expand} aria-label="Buscar en el menú" className="p-2">
        <svg className="h-5 w-5 text-gray-600" viewBox="0 0 20 20" fill="currentColor">
          <path
            fillRule="evenodd"
            d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z"
            clipRule="evenodd"
          />
        </svg>
      </button>
    )
  }

  return (
    <div className="flex items-center gap-2 flex-1 max-w-xs ml-2">
      <div className="relative flex-1">
        <svg
          className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400 pointer-events-none"
          viewBox="0 0 20 20"
          fill="currentColor"
          aria-hidden
        >
          <path
            fillRule="evenodd"
            d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z"
            clipRule="evenodd"
          />
        </svg>
        <input
          ref={inputRef}
          type="search"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder="Buscar…"
          aria-label="Buscar producto"
          className="w-full pl-8 pr-3 py-1.5 text-sm rounded-full border border-gray-200 focus:outline-none focus:ring-2 focus:ring-rush-red focus:border-transparent"
        />
      </div>
      <button
        onClick={collapse}
        className="text-sm text-gray-500 hover:text-gray-800 whitespace-nowrap transition-colors"
      >
        Cancelar
      </button>
    </div>
  )
}
