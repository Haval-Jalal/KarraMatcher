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
  await driver.getByRole('button', { name: 'Lägg upp' }).click()
  await expect(driver.getByText(departurePlace)).toBeVisible()

  // ---- En annan förälder skickar en förfrågan ------------------------------------------
  await login(requester, PARENT_B_EMAIL, matchPath)
  await expect(requester.getByText(departurePlace)).toBeVisible()
  await requester.getByRole('button', { name: 'Fråga om plats' }).click()
  await requester.getByRole('button', { name: 'Skicka förfrågan' }).click()
  await expect(requester.getByRole('button', { name: 'Återta förfrågan' })).toBeVisible()

  // ---- Föraren nekar med ett meddelande (ett tyst nej får inte förekomma) ---------------
  // Omladdning: refresh-cookien över HTTPS ger föraren en ny token, så förfrågan syns.
  await driver.reload()
  await driver.getByRole('button', { name: 'Neka' }).click()
  await driver.getByLabel('Meddelande').fill('Ändrade planer, kan tyvärr inte köra.')
  await driver.getByRole('button', { name: 'Skicka nekande' }).click()

  // ---- Den som frågade ser svaret ------------------------------------------------------
  await requester.reload()
  await expect(requester.getByText(/Förarens svar/)).toBeVisible()

  await driverContext.close()
  await requesterContext.close()
})
