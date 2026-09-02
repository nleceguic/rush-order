import { useEffect } from 'react'
import { PwaInstallBanner } from 'rush-order-pwa'

// PwaInstallBanner has no props - it renders only once the real
// usePwaInstall() hook has seen a native `beforeinstallprompt` event and the
// visit-count threshold. We drive the same hook through its real browser
// signals rather than reimplementing its markup: seed the use-counter
// synchronously (read on the child's mount effect) so it's already >= the
// threshold, then dispatch the real event from a parent effect - child
// effects run before parent effects, so PwaInstallBanner's own listener is
// registered by the time this fires.
localStorage.setItem('pwa-uses', '1')
localStorage.removeItem('pwa-dismissed-at')

function InstallableBanner() {
  useEffect(() => {
    window.dispatchEvent(new Event('beforeinstallprompt', { cancelable: true }))
  }, [])

  return <PwaInstallBanner />
}

export function Default() {
  return (
    <div style={{ position: 'relative', height: 160 }}>
      <InstallableBanner />
    </div>
  )
}
