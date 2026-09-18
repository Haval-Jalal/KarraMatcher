import { expect, test } from '@playwright/test'

import { PARENT_A_EMAIL, e2eMatchId, login } from './helpers'

/**
 * Flöde 4 (SPEC.md §9), v2: en inloggad förälder lägger till sitt barn på enheten → fyller
 * i mål efter matchen → ett märke låses upp. **Spelarkortet lämnar aldrig enheten (§KM.2).**
 *
 * I v1 kördes flödet helt utan konto. I v2 är appen stängd (§KM.3, `#191`): lagmenyn och
 * matchvyn kräver medlemskap, så föräldern loggar in. Det viktiga §KM.2-kravet är oförändrat
 * och vaktas här: spelarkortet skickar inget (POST/PUT/PATCH) och **inget anrop bär barnets
 * namn eller kortets innehåll** — inte ens för en inloggad medlem. Anrop fångas därför först
 * *efter* inloggningen, så att inloggningens egna anrop inte räknas med.
 */
test('spelarkortet lever på enheten och lämnar aldrig den', async ({ page }) => {
  const childName = 'E2E Barn'

  // Föräldern är medlem i laget (via prepare) — annars når hen inte lagmenyn eller matchen.
  await login(page, PARENT_A_EMAIL, '/lag/gul')

  // Fånga anrop först nu: inloggningens request-code/verify-code ska inte räknas.
  const apiRequests: { url: string; body: string }[] = []
  page.on('request', (request) => {
    if (request.url().includes('/api/')) {
      apiRequests.push({ url: request.url(), body: request.postData() ?? '' })
    }
  })

  // ---- Lägg till barn, helt på enheten -------------------------------------------------
  await page.goto('/spelarkort')

  await page.getByLabel('Namn eller smeknamn').fill(childName)
  await page.getByLabel('Lag (valfritt)').selectOption({ label: 'Gul' })
  await page.getByRole('button', { name: 'Lägg till' }).click()

  await expect(page.getByText(childName, { exact: true })).toBeVisible()

  // Överlever en omladdning: datan ligger i enhetens lagring, inte i minnet.
  await page.reload()
  await expect(page.getByText(childName, { exact: true })).toBeVisible()

  // §KM.2: spelarkortssidan skickar aldrig något (POST/PUT/PATCH) — laglistan till
  // "Lag"-menyn får hämtas (GET), men inget anrop får bära kortets innehåll (kontrolleras
  // nedan mot varje anrops kropp).
  const writes = apiRequests.filter((r) => r.body !== '')
  expect(writes).toHaveLength(0)

  // ---- Fyll i ett mål efter matchen → märke låses upp ----------------------------------
  const matchId = await e2eMatchId(page)
  await page.goto(`/handelse/${matchId}`)

  await expect(page.getByRole('heading', { name: 'Efter matchen' })).toBeVisible()

  // Ett mål räcker för märket "Gör ett mål".
  await page.getByRole('button', { name: `Öka Mål — ${childName}` }).click()

  await expect(page.getByRole('heading', { name: /Nytt märke/ })).toBeVisible()

  // Matchsidan hämtar matchdata, men inget anrop får bära barnets namn eller kortets innehåll.
  for (const request of apiRequests) {
    expect(request.body).not.toContain(childName)
  }
})
