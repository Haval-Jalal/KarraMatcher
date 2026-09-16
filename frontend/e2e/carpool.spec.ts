import { expect, test, type Browser, type Page } from '@playwright/test'

import { API, PARENT_A_EMAIL, PARENT_B_EMAIL, e2eMatchId, login } from './helpers'

/**
 * Flöde 5 (SPEC.md §9): en förälder lägger upp en skjuts → en annan skickar en förfrågan →
 * föraren nekar med ett meddelande → båda ser svaret.
 *
 * <h3>En färsk kontext per inloggad matchvy</h3>
 *
 * Appen avgör ägarskap av ett erbjudande server-side utifrån den inloggade sessionen
 * (`offer.isMine`). I den stängda appen (§KM.3, `#191`) kräver matchsidan dessutom
 * medlemskap — föräldrarna är vårdnadshavare i laget via prepare. Att nå matchen som ägare
 * kräver att token redan finns i minnet när listan hämtas — vilket en *första* inloggning på
 * en *färsk* kontext ger (den landar på matchen med token satt). En andra inloggning på en redan
 * inloggad kontext hänger i stället: appen klientsidigt-omdirigerar bort från inloggningen,
 * utan en `load`-händelse att vänta på. Därför en ny kontext varje gång en part behöver se
 * matchen inloggad.
 */
async function loggedInMatch(
  browser: Browser,
  email: string,
  matchPath: string,
): Promise<{ page: Page; close: () => Promise<void> }> {
  const context = await browser.newContext({ ignoreHTTPSErrors: true })
  const page = await context.newPage()
  await login(page, email, matchPath)
  return { page, close: () => context.close() }
}

test('samåkning: erbjudande, förfrågan och nekande med meddelande', async ({
  browser,
  request,
}) => {
  // Fyra inloggningar över en riktig stack — ge det gott om tid.
  test.setTimeout(120_000)

  await request.post(`${API}/api/v1/testing/reset-carpool`)

  // Matchens id från prepare (idempotent, returnerar matchId) — ingen schemaläsning behövs.
  const probe = await browser.newContext({ ignoreHTTPSErrors: true })
  const probePage = await probe.newPage()
  const matchId = await e2eMatchId(probePage)
  await probe.close()

  const matchPath = `/match/${matchId}`
  const departurePlace = `Kärra centrum ${Date.now()}`

  // ---- Föraren lägger upp en skjuts ----------------------------------------------------
  const driver = await loggedInMatch(browser, PARENT_A_EMAIL, matchPath)
  await driver.page.getByRole('button', { name: 'Erbjud skjuts' }).click()
  await driver.page.getByLabel('Var åker ni ifrån?').fill(departurePlace)
  const [offerResponse] = await Promise.all([
    driver.page.waitForResponse(
      (r) => r.url().endsWith('/carpool/offers') && r.request().method() === 'POST',
    ),
    driver.page.getByRole('button', { name: 'Lägg upp' }).click(),
  ])
  expect(offerResponse.status()).toBe(201)
  await expect(driver.page.getByText(departurePlace)).toBeVisible()
  await driver.close()

  // ---- En annan förälder skickar en förfrågan ------------------------------------------
  const requester = await loggedInMatch(browser, PARENT_B_EMAIL, matchPath)
  await expect(requester.page.getByText(departurePlace)).toBeVisible()
  await requester.page.getByRole('button', { name: 'Fråga om plats' }).click()
  const [requestResponse] = await Promise.all([
    requester.page.waitForResponse(
      (r) => r.url().endsWith('/requests') && r.request().method() === 'POST',
    ),
    requester.page.getByRole('button', { name: 'Skicka förfrågan' }).click(),
  ])
  expect(requestResponse.status()).toBe(201)
  await requester.close()

  // ---- Föraren ser förfrågan och nekar med ett meddelande (ett tyst nej får inte ske) ---
  const respondingDriver = await loggedInMatch(browser, PARENT_A_EMAIL, matchPath)
  await respondingDriver.page.getByRole('button', { name: 'Neka' }).click()
  await respondingDriver.page.getByLabel('Meddelande').fill('Ändrade planer, kan tyvärr inte köra.')
  const [denyResponse] = await Promise.all([
    respondingDriver.page.waitForResponse(
      (r) => r.url().endsWith('/deny') && r.request().method() === 'POST',
    ),
    respondingDriver.page.getByRole('button', { name: 'Skicka nekande' }).click(),
  ])
  expect(denyResponse.status()).toBe(204)
  await respondingDriver.close()

  // ---- Den som frågade ser svaret ------------------------------------------------------
  const returningRequester = await loggedInMatch(browser, PARENT_B_EMAIL, matchPath)
  await expect(returningRequester.page.getByText(/Förarens svar/)).toBeVisible()
  await returningRequester.close()
})
