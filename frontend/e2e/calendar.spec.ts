import { expect, test } from '@playwright/test'

import { API } from './helpers'

/**
 * Flöde 2 (SPEC.md §9): prenumerera på kalendern → matcherna finns i telefonens kalender.
 * Playwright kan inte följa `webcal://`, så vi vaktar länkens href och hämtar den riktiga
 * ICS-feeden direkt från backend.
 */
test('kan prenumerera på lagets kalender', async ({ page, request }) => {
  await page.goto('/lag/gul')

  const link = page.getByRole('link', { name: 'Prenumerera i kalendern' })
  await expect(link).toHaveAttribute('href', /^webcal:\/\/.*\/calendar\/gul\.ics$/)

  const response = await request.get(`${API}/calendar/gul.ics`)
  expect(response.ok()).toBeTruthy()
  expect(response.headers()['content-type']).toContain('text/calendar')

  const body = await response.text()
  expect(body).toContain('BEGIN:VCALENDAR')
  expect(body).toContain('END:VCALENDAR')
})
