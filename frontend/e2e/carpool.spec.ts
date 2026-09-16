import { expect, test, type Page } from '@playwright/test'

import { API, PARENT_A_EMAIL, PARENT_B_EMAIL, e2eMatchId, login } from './helpers'

/**
 * Flöde 5 (SPEC.md §9): en förälder lägger upp en skjuts → en annan skickar en förfrågan →
 * föraren nekar med ett meddelande → båda ser svaret. Två webbläsarkontexter, en per
 * förälder. Samåkningen nollställs först, så det finns exakt ett erbjudande att svara på.
 */

/**
 * Monterar matchsidan på nytt utan omladdning: navigera klientsidigt till lagets schema via
 * tillbaka-länken och sedan tillbaka in i matchen via dess länk. Båda stegen är länkklick, så
 * åtkomst-token lever kvar i minnet — och eftersom routen lämnas och monteras på nytt hämtas
 * samåkningen om *inloggad* (den publika erbjudande-listan saknar ägarinfo tills dess). Det är
 * så en part ser vad den andra just gjort, utan en omladdning som tappar sessionen.
 */
async function remountMatch(page: Page, matchId: string): Promise<void> {
  await page
    .getByRole('link', { name: /P2016 Gul/ })
    .first()
    .click()
  await page.waitForURL('**/lag/gul')
  await page.locator(`a[href="/match/${matchId}"]`).first().click()
  await page.waitForURL(`**/match/${matchId}`)
}

test('samåkning: erbjudande, förfrågan och nekande med meddelande', async ({
  browser,
  request,
}) => {
  test.setTimeout(60_000)

  await request.post(`${API}/api/v1/testing/reset-carpool`)

  const driverContext = await browser.newContext({ ignoreHTTPSErrors: true })
  const requesterContext = await browser.newContext({ ignoreHTTPSErrors: true })
  const driver = await driverContext.newPage()
  const requester = await requesterContext.newPage()

  const matchId = await e2eMatchId(driver)
  const matchPath = `/match/${matchId}`
  const departurePlace = `Kärra centrum ${Date.now()}`

  // ---- Föraren lägger upp en skjuts ----------------------------------------------------
  await login(driver, PARENT_A_EMAIL, matchPath)
  await driver.getByRole('button', { name: 'Erbjud skjuts' }).click()
  await driver.getByLabel('Var åker ni ifrån?').fill(departurePlace)
  const [offerResponse] = await Promise.all([
    driver.waitForResponse(
      (r) => r.url().endsWith('/carpool/offers') && r.request().method() === 'POST',
    ),
    driver.getByRole('button', { name: 'Lägg upp' }).click(),
  ])
  expect(offerResponse.status()).toBe(201)
  await expect(driver.getByText(departurePlace)).toBeVisible()

  // ---- En annan förälder skickar en förfrågan ------------------------------------------
  await login(requester, PARENT_B_EMAIL, matchPath)
  await expect(requester.getByText(departurePlace)).toBeVisible()
  await requester.getByRole('button', { name: 'Fråga om plats' }).click()
  const [requestResponse] = await Promise.all([
    requester.waitForResponse(
      (r) => r.url().endsWith('/requests') && r.request().method() === 'POST',
    ),
    requester.getByRole('button', { name: 'Skicka förfrågan' }).click(),
  ])
  expect(requestResponse.status()).toBe(201)

  // ---- Föraren ser förfrågan och nekar med ett meddelande (ett tyst nej får inte ske) ---
  await remountMatch(driver, matchId)
  // Diagnostik: vad ser föraren i samåkningssektionen efter omnavigeringen?
  console.log(
    'DRIVER SAMAKNING:',
    await driver
      .getByRole('region', { name: 'Samåkning' })
      .innerText()
      .catch(() => 'INGEN region'),
  )
  await driver.getByRole('button', { name: 'Neka' }).click()
  await driver.getByLabel('Meddelande').fill('Ändrade planer, kan tyvärr inte köra.')
  const [denyResponse] = await Promise.all([
    driver.waitForResponse((r) => r.url().endsWith('/deny') && r.request().method() === 'POST'),
    driver.getByRole('button', { name: 'Skicka nekande' }).click(),
  ])
  expect(denyResponse.status()).toBe(204)

  // ---- Den som frågade ser svaret ------------------------------------------------------
  await remountMatch(requester, matchId)
  await expect(requester.getByText(/Förarens svar/)).toBeVisible()

  await driverContext.close()
  await requesterContext.close()
})
