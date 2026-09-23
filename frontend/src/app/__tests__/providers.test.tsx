import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { App } from '@/app/App'
import { queryClient } from '@/app/queryClient'

describe('App', () => {
  it('monterar utan att krascha och möter en gäst med inloggningen', async () => {
    // Appen är stängd (§KM.3, `#242`): en gäst på rotadressen skickas till inloggningen i
    // stället för att se ett tomt schema. Att den monterar och grinden håller räcker här.
    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Logga in' })).toBeInTheDocument()
  })
})

describe('queryClient', () => {
  // Att provider-kedjan faktiskt fungerar bevisas först när en feature anropar
  // useQuery — det kommer i M1. Här kontrollerar vi de inställningar som är
  // medvetna val, så att de inte tyst ändras.
  it('hämtar inte om vid fönsterfokus', () => {
    expect(queryClient.getDefaultOptions().queries?.refetchOnWindowFocus).toBe(false)
  })

  it('har en staleTime som håller appen tyst på dålig täckning', () => {
    expect(queryClient.getDefaultOptions().queries?.staleTime).toBe(60_000)
  })
})
