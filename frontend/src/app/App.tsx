import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from '@tanstack/react-router'

import { queryClient } from '@/app/queryClient'
import { router } from '@/app/routes'
import { InstallBanner } from '@/components/InstallBanner'
import { UpdateBanner } from '@/components/UpdateBanner'
import { AuthProvider } from '@/features/auth'
import { SelectedTeamProvider } from '@/features/teams'

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <SelectedTeamProvider>
          <UpdateBanner />
          <InstallBanner />
          <RouterProvider router={router} />
        </SelectedTeamProvider>
      </AuthProvider>
    </QueryClientProvider>
  )
}
