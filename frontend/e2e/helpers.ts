import { expect, type Page } from '@playwright/test'

/** Backendens adress för test- och kalender-endpoints (går inte via Vite-proxyn). */
export const API = process.env.E2E_API_URL ?? 'http://localhost:5066'

/** Kontona som `POST /api/v1/testing/prepare` skapar. Fasta så flödena kan lita på dem. */
export const COACH_EMAIL = 'coach-e2e@test.local'
export const PARENT_A_EMAIL = 'parent-a-e2e@test.local'
export const PARENT_B_EMAIL = 'parent-b-e2e@test.local'

/** Tömmer brevlådan för en adress, så nästa kod som dyker upp garanterat är den nya. */
async function clearLoginCode(page: Page, email: string): Promise<void> {
  await page.request.delete(`${API}/api/v1/testing/code?email=${encodeURIComponent(email)}`)
}

/** Pollar tills en kod finns i brevlådan (den tömdes precis, så det är den färska). */
export async function fetchLoginCode(page: Page, email: string): Promise<string> {
  for (let attempt = 0; attempt < 80; attempt++) {
    const response = await page.request.get(
      `${API}/api/v1/testing/code?email=${encodeURIComponent(email)}`,
    )

    if (response.ok()) {
      const body = (await response.json()) as { code: string }
      return body.code
    }

    await page.waitForTimeout(100)
  }

  throw new Error(`Ingen inloggningskod fångades för ${email}.`)
}

/**
 * Loggar in via det riktiga UI:t och landar på `next`.
 *
 * Kontona har förnamn satt av prepare, så namnformuläret hoppas över. Navigeringen till
 * `next` sker klientsidigt (åtkomst-token lever i minnet), så anropande test ska sedan röra
 * sig med länkklick, inte page.goto — en full omladdning nollar tokenen.
 */
export async function login(page: Page, email: string, next: string): Promise<void> {
  await page.goto(`/logga-in?next=${encodeURIComponent(next)}`)

  // Töm en eventuell kvarliggande kod från en tidigare inloggning med samma konto, så att
  // koden vi sedan hämtar garanterat är den nya och inte en redan förbrukad.
  await clearLoginCode(page, email)

  await page.getByLabel('Mejladress').fill(email)
  await page.getByRole('button', { name: 'Skicka kod' }).click()

  const code = await fetchLoginCode(page, email)

  await page.getByLabel('Kod från mejlet').fill(code)
  await page.getByRole('button', { name: 'Logga in', exact: true }).click()

  // Poll:a sökvägen i stället för waitForURL: navigeringen till `next` sker klientsidigt
  // (pushState, ingen `load`-händelse), och waitForURL('load') hänger då ibland.
  await expect.poll(() => new URL(page.url()).pathname, { timeout: 15_000 }).toBe(next)
}

/**
 * Id:t på den framtida match prepare skapade (motståndare "E2E FC" i lag gul).
 *
 * Läses ur `POST /api/v1/testing/prepare` (idempotent, returnerar `matchId`) i stället för
 * ur schemat: i den stängda appen (§KM.3, `#191`) kräver `GET /teams/{slug}/matches` nu
 * inloggning + medlemskap, och id:t ska gå att hämta utan att först logga in.
 */
export async function e2eMatchId(page: Page): Promise<string> {
  const response = await page.request.post(`${API}/api/v1/testing/prepare`)
  expect(response.ok()).toBeTruthy()

  const body = (await response.json()) as { matchId: string }
  return body.matchId
}
