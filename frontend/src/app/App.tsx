import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from '@tanstack/react-router'

import { queryClient } from '@/app/queryClient'
import { router } from '@/app/routes'
import { ErrorBoundary } from '@/components/ErrorBoundary'
import { InstallBanner } from '@/components/InstallBanner'
import { UpdateBanner } from '@/components/UpdateBanner'
import { AuthProvider } from '@/features/auth'
import { SelectedTeamProvider } from '@/features/teams'

export function App() {
  // Ytterst: ett kast i en provider, i rot-layouten eller i routern själv fångas här i
  // stället för att bli en vit skärm (#389). Route-interna fel har sin egen gräns
  // (RouteError via routerns defaultErrorComponent).
  return (
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <SelectedTeamProvider>
            <UpdateBanner />
            <InstallBanner />
            <RouterProvider router={router} />
          </SelectedTeamProvider>
        </AuthProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  )
}
