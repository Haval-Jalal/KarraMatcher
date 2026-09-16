import { expect, test } from '@playwright/test'

/**
 * Flöde 1 (SPEC.md §9): öppna länk → välj lag → se nästa match → vägbeskrivning.
 * **Utan konto.** Det viktigaste kravet är att inget anrop bär en token.
 */
test('hittar nästa match utan konto och utan token', async ({ page }) => {
  const authHeaders: string[] = []

  page.on('request', (request) => {
    if (request.url().includes('/api/')) {
      authHeaders.push(request.headers()['authorization'] ?? '')
    }
  })

  // Ny kontext utan sparad lagvalslagring landar på lagväljaren.
  await page.goto('/')

  await page
    .getByRole('navigation', { name: 'Välj lag' })
    .getByRole('link', { name: 'Gul', exact: true })
    .click()

  await expect(page).toHaveURL(/\/lag\/gul/)

  await expect(page.getByRole('heading', { name: 'Nästa match' })).toBeVisible()

  await page.getByRole('link', { name: 'Visa matchen' }).click()

  await expect(page).toHaveURL(/\/match\//)
  await expect(page.getByRole('link', { name: /Vägbeskrivning/ })).toBeVisible()

  // Kärnan i flödet: en gäst ska aldrig ha skickat en Authorization-header.
  expect(authHeaders.length).toBeGreaterThan(0)
  expect(authHeaders.every((header) => header === '')).toBe(true)
})
