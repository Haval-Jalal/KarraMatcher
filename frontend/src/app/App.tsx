import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from '@tanstack/react-router'

import { queryClient } from '@/app/queryClient'
import { router } from '@/app/routes'
import { UpdateBanner } from '@/components/UpdateBanner'
import { SelectedTeamProvider } from '@/features/teams'

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <SelectedTeamProvider>
        <UpdateBanner />
        <RouterProvider router={router} />
      </SelectedTeamProvider>
    </QueryClientProvider>
  )
}
