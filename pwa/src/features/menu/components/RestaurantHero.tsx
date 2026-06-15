import type { QRData } from '../types'
import { BlurImage } from './BlurImage'

const LOCALE_LABELS: Record<string, string> = {
  es: 'Español',
  en: 'English',
  ca: 'Català',
  fr: 'Français',
  de: 'Deutsch',
  it: 'Italiano',
  pt: 'Português',
}

interface RestaurantHeroProps {
  data:           QRData
  locale:         string
  onLocaleChange: (locale: string) => void
  onViewMenu:     () => void
}

export function RestaurantHero({ data, locale, onLocaleChange, onViewMenu }: RestaurantHeroProps) {
  return (
    <div>
      {/* Cover photo */}
      <div className="relative">
        {data.coverImageUrl !== undefined ? (
          <BlurImage
            src={data.coverImageUrl}
            alt={`Portada de ${data.restaurantName}`}
            className="h-52 w-full"
            sizes="100vw"
          />
        ) : (
          <div className="h-52 w-full bg-gradient-to-br from-rush-red to-rush-dark" />
        )}

        {/* Language selector */}
        {data.availableLocales.length > 1 && (
          <div className="absolute top-3 right-3">
            <select
              value={locale}
              onChange={(e) => onLocaleChange(e.target.value)}
              className="rounded-full bg-white/90 backdrop-blur-sm border-0 text-xs font-medium px-3 py-1.5 shadow focus:ring-2 focus:ring-rush-red"
            >
              {data.availableLocales.map((l) => (
                <option key={l} value={l}>
                  {LOCALE_LABELS[l] ?? l.toUpperCase()}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* Logo */}
      <div className="-mt-10 flex justify-center">
        {data.logoUrl !== undefined ? (
          <img
            src={data.logoUrl}
            alt={`Logo de ${data.restaurantName}`}
            className="h-20 w-20 rounded-full border-4 border-white shadow-lg object-cover"
            loading="eager"
          />
        ) : (
          <div className="h-20 w-20 rounded-full border-4 border-white shadow-lg bg-rush-red flex items-center justify-center">
            <span className="text-2xl font-bold text-white" aria-hidden>
              {data.restaurantName.charAt(0).toUpperCase()}
            </span>
          </div>
        )}
      </div>

      {/* Restaurant info */}
      <div className="text-center px-6 pt-3 pb-5">
        <h1 className="text-2xl font-bold text-rush-dark">{data.restaurantName}</h1>
        {data.welcomeMessage !== undefined && (
          <p className="mt-1.5 text-gray-500 text-sm leading-relaxed max-w-xs mx-auto">
            {data.welcomeMessage}
          </p>
        )}
      </div>

      {/* CTA */}
      <div className="flex justify-center pb-8">
        <button
          onClick={onViewMenu}
          className="flex items-center gap-2 bg-rush-red text-white font-semibold px-8 py-3 rounded-full shadow-md hover:bg-rush-red-hover active:scale-95 transition-all"
        >
          Ver el menú
          <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor" aria-hidden>
            <path
              fillRule="evenodd"
              d="M16.707 10.293a1 1 0 010 1.414l-6 6a1 1 0 01-1.414 0l-6-6a1 1 0 111.414-1.414L9 14.586V3a1 1 0 012 0v11.586l4.293-4.293a1 1 0 011.414 0z"
              clipRule="evenodd"
            />
          </svg>
        </button>
      </div>
    </div>
  )
}
