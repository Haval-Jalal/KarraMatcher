import { expect, test } from '@playwright/test'

import { API, PARENT_A_EMAIL, PARENT_B_EMAIL, e2eMatchId, login } from './helpers'

/**
 * Flöde 5 (SPEC.md §9): en förälder lägger upp en skjuts → en annan skickar en förfrågan →
 * föraren nekar med ett meddelande → båda ser svaret. Två webbläsarkontexter, en per
 * förälder. Samåkningen nollställs först, så det finns exakt ett erbjudande att svara på.
 */
test('samåkning: erbjudande, förfrågan och nekande med meddelande', async ({
  browser,
  request,
}) => {
  // Flera inloggningar och två kontexter — ge det gott om tid.
  test.setTimeout(90_000)

  await request.post(`${API}/api/v1/testing/reset-carpool`)

  const driverContext = await browser.newContext({ ignoreHTTPSErrors: true })
  const requesterContext = await browser.newContext({ ignoreHTTPSErrors: true })
  const driver = await driverContext.newPage()
  const requester = await requesterContext.newPage()

  const matchPath = `/match/${await e2eMatchId(driver)}`
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

  // ---- Föraren nekar med ett meddelande (ett tyst nej får inte förekomma) ---------------
  // Ny inloggning i stället för reload: då landar föraren på matchen med token redan i
  // minnet, så samåkningshämtningen inte kapplöper med sessionsåterställningen på den
  // publika sidan. Förfrågan syns direkt.
  await login(driver, PARENT_A_EMAIL, matchPath)
  await driver.getByRole('button', { name: 'Neka' }).click()
  await driver.getByLabel('Meddelande').fill('Ändrade planer, kan tyvärr inte köra.')
  const [denyResponse] = await Promise.all([
    driver.waitForResponse((r) => r.url().endsWith('/deny') && r.request().method() === 'POST'),
    driver.getByRole('button', { name: 'Skicka nekande' }).click(),
  ])
  expect(denyResponse.status()).toBe(204)

  // ---- Den som frågade ser svaret ------------------------------------------------------
  await login(requester, PARENT_B_EMAIL, matchPath)
  await expect(requester.getByText(/Förarens svar/)).toBeVisible()

  await driverContext.close()
  await requesterContext.close()
})
