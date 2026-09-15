import { expect, type Page } from '@playwright/test'

/** Backendens adress för test- och kalender-endpoints (går inte via Vite-proxyn). */
export const API = process.env.E2E_API_URL ?? 'http://localhost:5066'

/** Kontona som `POST /api/v1/testing/prepare` skapar. Fasta så flödena kan lita på dem. */
export const COACH_EMAIL = 'coach-e2e@test.local'
export const PARENT_A_EMAIL = 'parent-a-e2e@test.local'
export const PARENT_B_EMAIL = 'parent-b-e2e@test.local'

export const E2E_TEAM_SLUG = 'gul'
export const E2E_OPPONENT = 'E2E FC'

/** Hämtar den senaste inloggningskoden ur testbrevlådan. Pollar — mejlet "skickas" async. */
export async function fetchLoginCode(page: Page, email: string): Promise<string> {
  for (let attempt = 0; attempt < 20; attempt++) {
    const response = await page.request.get(
      `${API}/api/v1/testing/code?email=${encodeURIComponent(email)}`,
    )

    if (response.ok()) {
      const body = (await response.json()) as { code: string }
      return body.code
    }

    await page.waitForTimeout(250)
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

  await page.getByLabel('Mejladress').fill(email)
  await page.getByRole('button', { name: 'Skicka kod' }).click()

  const code = await fetchLoginCode(page, email)

  await page.getByLabel('Kod från mejlet').fill(code)
  await page.getByRole('button', { name: 'Logga in', exact: true }).click()

  await page.waitForURL(`**${next}`)
}

/** Id:t på den framtida match prepare skapade (motståndare "E2E FC" i lag gul). */
export async function e2eMatchId(page: Page): Promise<string> {
  const response = await page.request.get(`${API}/api/v1/teams/${E2E_TEAM_SLUG}/matches`)
  expect(response.ok()).toBeTruthy()

  const body = (await response.json()) as { matches: { id: string; opponent: string }[] }
  const match = body.matches.find((m) => m.opponent === E2E_OPPONENT)

  if (!match) {
    throw new Error('E2E-matchen saknas — kördes prepare?')
  }

  return match.id
}
