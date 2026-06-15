import { useTranslation } from 'react-i18next'

export function SkipLink() {
  const { t } = useTranslation()
  return (
    <a
      href="#main-content"
      className={[
        'sr-only focus:not-sr-only',
        'focus:fixed focus:top-4 focus:left-4 focus:z-[9999]',
        'focus:bg-rush-red focus:text-white focus:px-4 focus:py-2',
        'focus:rounded-lg focus:font-semibold focus:text-sm',
        'focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-rush-red',
      ].join(' ')}
    >
      {t('skipLink')}
    </a>
  )
}
