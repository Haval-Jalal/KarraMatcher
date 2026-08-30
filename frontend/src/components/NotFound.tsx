import { Link } from '@tanstack/react-router'

import { useDocumentTitle } from '@/lib/useDocumentTitle'

/**
 * Visas för adresser som inte finns. Texten är på svenska och säger vad
 * användaren kan göra — inte bara att något gick fel (CLAUDE.md §KM.9).
 */
export function NotFound() {
  useDocumentTitle('Sidan finns inte')

  return (
    <main>
      <h1>Sidan finns inte</h1>
      <p>Länken kan vara gammal eller felstavad.</p>
      <Link to="/">Till startsidan</Link>
    </main>
  )
}
