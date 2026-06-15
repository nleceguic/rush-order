import { useTranslation } from 'react-i18next'
import { usePwaInstall } from '@shared/hooks/usePwaInstall'
import { useAnalytics } from '@shared/hooks/useAnalytics'

export function PwaInstallBanner() {
  const { t }                         = useTranslation()
  const { canInstall, install, dismiss } = usePwaInstall()
  const { track }                     = useAnalytics()

  if (!canInstall) return null

  const handleInstall = async () => {
    await install()
    track('app_installed')
  }

  return (
    <div
      role="banner"
      aria-label={t('pwa.installTitle')}
      className="fixed bottom-20 inset-x-4 z-40 rounded-2xl bg-rush-dark text-white shadow-2xl flex items-center gap-3 px-4 py-3"
      style={{ animation: 'scale-in 0.3s ease' }}
    >
      {/* Rush Order icon */}
      <div className="h-10 w-10 rounded-xl bg-rush-red flex items-center justify-center flex-shrink-0">
        <svg className="h-6 w-6 text-white" viewBox="0 0 24 24" fill="none" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5}
            d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"
          />
        </svg>
      </div>

      <div className="flex-1 min-w-0">
        <p className="font-bold text-sm leading-tight">{t('pwa.installTitle')}</p>
        <p className="text-xs text-white/70 leading-tight">{t('pwa.installDesc')}</p>
      </div>

      <div className="flex flex-col gap-1.5 flex-shrink-0">
        <button
          type="button"
          onClick={handleInstall}
          className="text-xs font-bold px-3 py-1.5 rounded-lg bg-rush-red hover:bg-rush-red-hover transition-colors"
        >
          {t('pwa.install')}
        </button>
        <button
          type="button"
          onClick={dismiss}
          className="text-xs text-white/60 hover:text-white/90 text-center transition-colors"
        >
          {t('pwa.later')}
        </button>
      </div>
    </div>
  )
}
