import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement, ReactNode } from 'react'

import { SelectedTeamProvider } from '@/features/teams'

/**
 * Renderar med samma providerkedja som appen.
 *
 * En egen QueryClient per test, så att cachen aldrig läcker mellan testfall — annars hade
 * ett test kunnat se lagen som ett tidigare test hämtade och gå grönt av fel skäl.
 * `retry: false` gör att ett feltillstånd syns direkt i stället för efter tre försök.
 */
export function renderWithProviders(ui: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  })

  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <SelectedTeamProvider>{children}</SelectedTeamProvider>
      </QueryClientProvider>
    )
  }

  return { ...render(ui, { wrapper: Wrapper }), queryClient }
}
