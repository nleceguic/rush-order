import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import es from './locales/es.json'
import en from './locales/en.json'
import ca from './locales/ca.json'
import fr from './locales/fr.json'

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      es: { translation: es },
      en: { translation: en },
      ca: { translation: ca },
      fr: { translation: fr },
    },
    fallbackLng:   'es',
    supportedLngs: ['es', 'en', 'ca', 'fr'],
    detection: {
      order:  ['localStorage', 'navigator'],
      caches: ['localStorage'],
    },
    interpolation: {
      escapeValue: false,
    },
  })

export default i18n
