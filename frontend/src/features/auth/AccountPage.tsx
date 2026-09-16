import { Link, useNavigate } from '@tanstack/react-router'
import { useEffect, useState } from 'react'

import { ConsentSection } from '@/features/consent'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { getProfile, type AccountProfile } from './authApi'
import { DataExportSection } from './DataExportSection'
import { DeleteAccountSection } from './DeleteAccountSection'
import { NameForm } from './NameForm'
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
  const { email, refresh, signOut } = useAuth()
  const navigate = useNavigate()

  const [profile, setProfile] = useState<AccountProfile | null>(null)
  const [editing, setEditing] = useState(false)

  useDocumentTitle('Mitt konto')

  /*
   * Profilen hamtas en gang nar sidan oppnas. En egen hook vore overarbetat for ett varde
   * som andras har och ingen annanstans -- men avbrytningen behovs anda, annars satter en
   * sen hamtning tillstand i en komponent som redan lamnat skarmen.
   */
  useEffect(() => {
    let cancelled = false

    void (async () => {
      try {
        const loaded = await getProfile()

        if (!cancelled) {
          setProfile(loaded)
        }
      } catch {
        // Namnet ar inte det viktigaste pa sidan. Gar det inte att hamta star resten kvar.
      }
    })()

    return () => {
      cancelled = true
    }
  }, [])

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

      <section aria-labelledby="mitt-namn">
        <h2 className="match-list__title" id="mitt-namn">
          Mitt namn
        </h2>

        {/*
          Texten säger vem som ser namnet, eftersom det är den frågan en förälder har innan
          hen skriver in det. Bara inloggade i laget — aldrig den som bara tittar på
          matchtiden (§KM.3).
        */}
        <p className="state">
          Namnet visas i samåkningen, så att den som frågar om en plats vet vem som kör. Det syns
          bara för inloggade i laget.
        </p>

        {editing || profile?.needsName === true ? (
          <NameForm
            profile={profile}
            submitLabel="Spara namn"
            onSaved={(saved) => {
              setProfile(saved)
              setEditing(false)
            }}
            {...(editing
              ? {
                  onSkip: () => {
                    setEditing(false)
                  },
                }
              : {})}
          />
        ) : (
          <div className="actions">
            <p className="account__name">{profile?.displayName ?? 'Inget namn ifyllt'}</p>
            <button
              type="button"
              className="button button--action"
              onClick={() => {
                setEditing(true)
              }}
            >
              Ändra namn
            </button>
          </div>
        )}
      </section>

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

      <section aria-labelledby="integritet">
        <h2 className="match-list__title" id="integritet">
          Integritet
        </h2>
        <p className="state">
          Vad appen sparar, var, hur länge och hur du raderar det står i klartext på en egen sida:{' '}
          <Link to="/integritet">Så hanteras dina uppgifter</Link>.
        </p>
      </section>

      <ConsentSection />

      <DataExportSection />

      <DeleteAccountSection
        onDeleted={() => {
          // Kontot ar borta -- las om tillstandet sa appen inte star kvar som inloggad.
          refresh()
          void navigate({ to: '/' })
        }}
      />
    </main>
  )
}
