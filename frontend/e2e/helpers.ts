import { expect, type Page } from '@playwright/test'

/** Backendens adress för test- och kalender-endpoints (går inte via Vite-proxyn). */
export const API = process.env.E2E_API_URL ?? 'http://localhost:5066'

/** Kontona som `POST /api/v1/testing/prepare` skapar. Fasta så flödena kan lita på dem. */
export const COACH_EMAIL = 'coach-e2e@test.local'
export const PARENT_A_EMAIL = 'parent-a-e2e@test.local'
export const PARENT_B_EMAIL = 'parent-b-e2e@test.local'

export const E2E_TEAM_SLUG = 'gul'
export const E2E_OPPONENT = 'E2E FC'

/** Den kod brevlådan har för en adress just nu, eller null om ingen. */
async function peekLoginCode(page: Page, email: string): Promise<string | null> {
  const response = await page.request.get(
    `${API}/api/v1/testing/code?email=${encodeURIComponent(email)}`,
  )

  if (!response.ok()) {
    return null
  }

  const body = (await response.json()) as { code: string }
  return body.code
}

/**
 * Hämtar en <em>ny</em> inloggningskod — en som skiljer sig från <paramref>excluding</paramref>.
 *
 * Brevlådan är nycklad per adress och återanvänds över flera inloggningar med samma konto.
 * En enkel "läs senaste" skulle därför kunna returnera den <em>förra</em> inloggningens kod
 * innan den nya hunnit lagras, och verifieringen skulle misslyckas. Genom att vänta på en kod
 * som skiljer sig från den föregående får testet alltid den färska.
 */
export async function fetchFreshLoginCode(
  page: Page,
  email: string,
  excluding: string | null,
): Promise<string> {
  for (let attempt = 0; attempt < 80; attempt++) {
    const code = await peekLoginCode(page, email)

    if (code !== null && code !== excluding) {
      return code
    }

    await page.waitForTimeout(100)
  }

  throw new Error(`Ingen ny inloggningskod fångades för ${email}.`)
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

  // Den kod som eventuellt ligger kvar från en tidigare inloggning med samma konto — vi
  // väntar sedan på en som skiljer sig från den, så vi aldrig fyller i en förbrukad kod.
  const previous = await peekLoginCode(page, email)

  await page.getByLabel('Mejladress').fill(email)
  await page.getByRole('button', { name: 'Skicka kod' }).click()

  const code = await fetchFreshLoginCode(page, email, previous)

  await page.getByLabel('Kod från mejlet').fill(code)
  await page.getByRole('button', { name: 'Logga in', exact: true }).click()

  // Poll:a sökvägen i stället för waitForURL: navigeringen till `next` sker klientsidigt
  // (pushState, ingen `load`-händelse), och waitForURL('load') hänger då ibland.
  await expect.poll(() => new URL(page.url()).pathname, { timeout: 15_000 }).toBe(next)
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
