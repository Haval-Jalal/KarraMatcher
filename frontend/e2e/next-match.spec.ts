import { expect, test } from '@playwright/test'

import { PARENT_A_EMAIL, login } from './helpers'

/**
 * Flöde 1 (SPEC.md §9), v2: en inloggad **medlem** öppnar laget → ser nästa match →
 * vägbeskrivning.
 *
 * I v1 var kravet "utan konto, aldrig en token" — den öppna appen. I v2 är appen stängd
 * (§KM.3, `#191`): schemat kräver inloggning och medlemskap. Här loggar en vårdnadshavare i
 * laget in och når matchen; en gäst nekas, vilket vaktas av backendens `GuestAccessTests`.
 */
test('inloggad medlem hittar nästa match och vägbeskrivning', async ({ page }) => {
  await login(page, PARENT_A_EMAIL, '/lag/gul')

  await expect(page.getByRole('heading', { name: 'Nästa match' })).toBeVisible()

  // Klick, inte page.goto: navigeringen är klientsidig och en full omladdning nollar tokenen.
  await page.getByRole('link', { name: 'Visa matchen' }).click()

  await expect(page).toHaveURL(/\/handelse\//)
  await expect(page.getByRole('link', { name: /Vägbeskrivning/ })).toBeVisible()
})
