import { useQueryClient } from '@tanstack/react-query'
import { useParams } from '@tanstack/react-router'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { MatchList, teamMatchesQueryKey, useTeamMatches, type Match } from '@/features/matches'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { cancelMatch, createMatch, deleteMatch, updateMatch } from './adminApi'
import { MatchForm } from './MatchForm'

/**
 * Tränarens vy för ett lag.
 *
 * <h3>Vad den ska kännas som</h3>
 *
 * Det här görs stående vid en plan, på en telefon, ofta med ett barn i handen. Krångel här
 * är den enskilt största risken för att appen aldrig fylls med innehåll — och en tom app är
 * en app ingen öppnar igen.
 *
 * <h3>Ändringen syns direkt</h3>
 *
 * Efter varje ändring ogiltigförklaras lagets matchfråga, så den publika listan visar det
 * nya utan omladdning. Tränaren ska kunna se att det blev rätt, i samma vy.
 */
export function CoachMatchesPage() {
  const { slug } = useParams({ from: '/lag/$slug/tranare' })
  const { canManage } = useAuth()
  const queryClient = useQueryClient()
  const { data, isPending } = useTeamMatches(slug)

  const [editing, setEditing] = useState<Match | null>(null)
  const [adding, setAdding] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState<Match | null>(null)

  useDocumentTitle('Sköt laget')

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: teamMatchesQueryKey(slug) })
  }

  if (!canManage(slug)) {
    /*
     * Servern avgor vad nagon far gora -- det har avgor bara vad som visas. En tranare for
     * Gul som skriver in Blas adress moter alltsa den har texten, och skulle hen anda
     * skicka ett anrop svarar servern 403.
     */
    return (
      <main>
        <header className="app-header">
          <h1>Sköt laget</h1>
        </header>
        <p className="state state--error" role="alert">
          Du sköter inte det här laget. Kontakta klubben om det borde vara tvärtom.
        </p>
      </main>
    )
  }

  return (
    <main>
      <header className="app-header">
        <h1>Sköt laget</h1>
        <p className="app-header__subtitle">
          Ändringar syns direkt för föräldrarna, och kalenderprenumerationerna uppdateras.
        </p>
      </header>

      {adding || editing !== null ? (
        <MatchForm
          {...(editing !== null ? { existing: editing } : {})}
          onSubmit={async (input) => {
            if (editing !== null) {
              await updateMatch(slug, editing.id, input)
            } else {
              await createMatch(slug, input)
            }

            await refresh()
            setAdding(false)
            setEditing(null)
          }}
          onCancel={() => {
            setAdding(false)
            setEditing(null)
          }}
        />
      ) : (
        <div className="actions">
          <button
            type="button"
            className="button"
            onClick={() => {
              setAdding(true)
            }}
          >
            Lägg till match
          </button>
        </div>
      )}

      <h2 className="match-list__title">Lagets matcher</h2>

      {isPending ? (
        <p className="state" role="status">
          Hämtar matcherna…
        </p>
      ) : (
        <>
          <MatchList matches={data?.matches ?? []} now={new Date().toISOString()} />

          <ul className="coach-actions">
            {(data?.matches ?? []).map((match) => (
              <li key={match.id} className="coach-actions__row">
                <span className="coach-actions__label">{match.opponent}</span>

                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setEditing(match)
                  }}
                >
                  Ändra
                </button>

                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    void (async () => {
                      await cancelMatch(slug, match.id)
                      await refresh()
                    })()
                  }}
                >
                  Ställ in
                </button>

                <button
                  type="button"
                  className="button button--danger"
                  onClick={() => {
                    setConfirmDelete(match)
                  }}
                >
                  Ta bort
                </button>
              </li>
            ))}
          </ul>
        </>
      )}

      {confirmDelete !== null && (
        <section className="danger-zone">
          <h2>Ta bort matchen mot {confirmDelete.opponent}?</h2>

          <p className="state" role="alert">
            Matchen försvinner helt, även ur föräldrarnas kalendrar.{' '}
            <strong>Ska matchen ställas in ska du välja Ställ in i stället</strong> — då blir den
            kvar i kalendern, markerad som inställd.
          </p>

          <div className="actions">
            <button
              type="button"
              className="button"
              onClick={() => {
                setConfirmDelete(null)
              }}
            >
              Avbryt
            </button>
            <button
              type="button"
              className="button button--danger"
              onClick={() => {
                void (async () => {
                  await deleteMatch(slug, confirmDelete.id)
                  await refresh()
                  setConfirmDelete(null)
                })()
              }}
            >
              Ja, ta bort matchen
            </button>
          </div>
        </section>
      )}
    </main>
  )
}
