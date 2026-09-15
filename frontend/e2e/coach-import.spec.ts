import { expect, test } from '@playwright/test'

import { COACH_EMAIL, login } from './helpers'

/**
 * Flöde 3 (SPEC.md §9): tränare loggar in → klistrar in schema → granskar → sparar.
 * En framtida match läggs in via massinlägget; en ny paste varje körning (unikt datum +
 * motståndare) så att den inte krockar med en tidigare körnings rad.
 */
test('tränare loggar in och massinlägger en match', async ({ page }) => {
  await login(page, COACH_EMAIL, '/lag/gul/tranare')

  await expect(page.getByRole('heading', { name: 'Klistra in hela schemat' })).toBeVisible()

  // Unikt per körning så raden inte redan finns (dubblett hoppas över).
  const stamp = Date.now()
  const opponent = `E2E Import ${stamp}`
  const paste =
    'Datum\tTid\tLag\tMotståndare\tPlats\n' +
    `2026-11-15\t14:00\tGul\t${opponent}\tKlarebergsvallen 3`

  await page.getByLabel('Inklistrat schema').fill(paste)
  await page.getByRole('button', { name: 'Granska' }).click()

  // Efter granskning dyker en spara-knapp upp om minst en rad kan läggas till.
  const save = page.getByRole('button', { name: /Lägg till \d+ match/ })
  await expect(save).toBeVisible()
  await save.click()

  await expect(page.getByText(/tillagd|tillagda/)).toBeVisible()
})
