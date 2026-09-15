import { expect, test } from '@playwright/test'

import { e2eMatchId } from './helpers'

/**
 * Flöde 4 (SPEC.md §9): förälder lägger till sitt barn på enheten → fyller i mål efter
 * matchen → ett märke låses upp. **Inget konto, inget nätverksanrop** (§KM.2).
 *
 * Två saker vaktas: att spelarkortet fungerar helt utan att röra `/api`, och — när
 * resultatet fylls i på matchsidan (som i sig hämtar matchdata) — att inget anrop bär
 * barnets namn eller kortets innehåll.
 */
test('spelarkortet lever på enheten och lämnar aldrig den', async ({ page }) => {
  const childName = 'E2E Barn'
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

  // Kärnan i §KM.2: spelarkortssidan har inte rört API:t över huvud taget.
  expect(apiRequests.length).toBe(0)

  // ---- Fyll i ett mål efter matchen → märke låses upp ----------------------------------
  const matchId = await e2eMatchId(page)
  await page.goto(`/match/${matchId}`)

  await expect(page.getByRole('heading', { name: 'Efter matchen' })).toBeVisible()

  // Ett mål räcker för märket "Gör ett mål".
  await page.getByRole('button', { name: `Öka Mål — ${childName}` }).click()

  await expect(page.getByRole('heading', { name: /Nytt märke/ })).toBeVisible()

  // Matchsidan hämtar matchdata, men inget anrop får bära barnets namn eller kortets innehåll.
  for (const request of apiRequests) {
    expect(request.body).not.toContain(childName)
  }
})
