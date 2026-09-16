import { request } from '@playwright/test'

import { API } from './helpers'

/**
 * Förbereder databasen en gång före hela sviten: skapar tränarkonto + roll, två
 * föräldrakonton och en framtida match (allt idempotent, via den test-gejtade
 * `POST /api/v1/testing/prepare`). Utan det här skulle de inloggade flödena inte ha någon
 * identitet att logga in som, och nästa-match-flödet ingen framtida match att lita på.
 */
export default async function globalSetup(): Promise<void> {
  const context = await request.newContext()

  const response = await context.post(`${API}/api/v1/testing/prepare`)

  if (!response.ok()) {
    throw new Error(`Kunde inte förbereda E2E-data: ${response.status()} ${await response.text()}`)
  }

  await context.dispose()
}
