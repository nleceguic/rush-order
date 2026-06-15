import { useState, useEffect, useCallback } from 'react'

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

const USES_KEY       = 'pwa-uses'
const DISMISS_KEY    = 'pwa-dismissed-at'
const REQUIRED_USES  = 2
const DISMISS_MS     = 7 * 24 * 60 * 60 * 1000 // 7 days

export function usePwaInstall() {
  const [prompt,    setPrompt]    = useState<BeforeInstallPromptEvent | null>(null)
  const [installed, setInstalled] = useState(false)
  const [usesCount, setUsesCount] = useState(0)

  useEffect(() => {
    // Increment use counter once per mount
    const uses = parseInt(localStorage.getItem(USES_KEY) ?? '0') + 1
    localStorage.setItem(USES_KEY, String(uses))
    setUsesCount(uses)

    const dismissedAt = localStorage.getItem(DISMISS_KEY)
    if (dismissedAt !== null && Date.now() - parseInt(dismissedAt) < DISMISS_MS) return

    const handler = (e: Event) => {
      e.preventDefault()
      setPrompt(e as BeforeInstallPromptEvent)
    }

    window.addEventListener('beforeinstallprompt', handler)
    window.addEventListener('appinstalled', () => {
      setInstalled(true)
      setPrompt(null)
    })

    return () => window.removeEventListener('beforeinstallprompt', handler)
  }, [])

  const install = useCallback(async (): Promise<void> => {
    if (prompt === null) return
    await prompt.prompt()
    const { outcome } = await prompt.userChoice
    if (outcome === 'accepted') setInstalled(true)
    setPrompt(null)
  }, [prompt])

  const dismiss = useCallback((): void => {
    localStorage.setItem(DISMISS_KEY, String(Date.now()))
    setPrompt(null)
  }, [])

  const shouldShow = prompt !== null && !installed && usesCount >= REQUIRED_USES

  return { canInstall: shouldShow, install, dismiss, installed }
}
