import { screen } from '@testing-library/react'
import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from '@tanstack/react-router'
import { describe, expect, it } from 'vitest'

import { NotFound } from '@/components/NotFound'
import { StartPage } from '@/features/start/StartPage'
import { renderWithProviders } from '@/test/renderWithProviders'

/**
 * Bygger samma routeträd som appen, men med ett minneshistorik så att vi kan
 * styra adressen. Klientsidig routing går inte att verifiera med ett HTTP-anrop —
 * servern svarar med index.html oavsett adress.
 */
function renderAt(path: string) {
  const rootRoute = createRootRoute({
    component: () => <Outlet />,
    notFoundComponent: NotFound,
  })

  const startRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    component: StartPage,
  })

  const router = createRouter({
    routeTree: rootRoute.addChildren([startRoute]),
    history: createMemoryHistory({ initialEntries: [path] }),
  })

  // Routern är typad mot appens egna träd; här bygger vi ett eget för testet.
  // Providerkedjan behövs eftersom startsidan hämtar lagen.
  renderWithProviders(<RouterProvider router={router as never} />)
}

describe('routing', () => {
  it('visar startsidan på rotadressen', async () => {
    renderAt('/')

    expect(await screen.findByRole('heading', { name: 'Kärra Matcher' })).toBeInTheDocument()
  })

  it('visar 404-sidan för en adress som inte finns', async () => {
    renderAt('/finns-inte')

    expect(await screen.findByRole('heading', { name: 'Sidan finns inte' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Till startsidan' })).toBeInTheDocument()
  })
})
