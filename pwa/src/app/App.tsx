import { RouterProvider } from 'react-router-dom'
import { Providers } from './providers'
import { router } from './routes'
import { ToastContainer } from '@shared/components/Toast'
import { SkipLink } from '@shared/components/SkipLink'
import { PwaInstallBanner } from '@shared/components/PwaInstallBanner'

export default function App() {
  return (
    <Providers>
      <SkipLink />
      <RouterProvider router={router} />
      <ToastContainer />
      <PwaInstallBanner />
    </Providers>
  )
}
