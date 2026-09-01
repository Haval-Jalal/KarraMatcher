import { useNavigate } from '@tanstack/react-router'

import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { useAuth } from './useAuth'

/**
 * Kontosidan — den första skyddade vyn.
 *
 * <para>
 * Innehållet växer i M5 (samåkning) och M3 (tränarens lag). Just nu visar den vem som är
 * inloggad och låter en logga ut, vilket räcker för att skyddet ska gå att pröva och för
 * att en förälder ska kunna se att hen faktiskt är inloggad.
 * </para>
 */
export function AccountPage() {
  const { email, signOut } = useAuth()
  const navigate = useNavigate()

  useDocumentTitle('Mitt konto')

  return (
    <main>
      <header className="app-header">
        <h1>Mitt konto</h1>
        <p className="app-header__subtitle">Inloggad som {email ?? 'okänd adress'}</p>
      </header>

      <p className="state">
        Kontot används för samåkning och för tränarnas funktioner. Matchtider, kalender och
        vägbeskrivning fungerar utan det — även för den som aldrig loggar in.
      </p>

      <div className="actions">
        <button
          type="button"
          className="button"
          onClick={() => {
            void (async () => {
              await signOut()
              await navigate({ to: '/' })
            })()
          }}
        >
          Logga ut
        </button>
      </div>
    </main>
  )
}
