import { useId, useRef, useState } from 'react'

import { CreateActivity } from '@/features/activities'
import { ApplicationsPanel } from '@/features/applications'
import { useAuth } from '@/features/auth'
import { BarnOchLag } from '@/features/children'
import { ClubVenueSettings } from '@/features/clubs'
import { ApiError } from '@/lib/api'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { AdminOverview } from './AdminOverview'
import { InvitationsPanel } from './InvitationsPanel'
import { useMyTrupper } from './useInvitations'

const SECTIONS = [
  'oversikt',
  'aktiviteter',
  'barn',
  'inbjudningar',
  'ansokningar',
  'installningar',
] as const
type SectionKey = (typeof SECTIONS)[number]

const LABEL: Record<SectionKey, string> = {
  oversikt: 'Översikt',
  aktiviteter: 'Aktiviteter',
  barn: 'Barn & lag',
  inbjudningar: 'Inbjudningar',
  ansokningar: 'Ansökningar',
  installningar: 'Inställningar',
}

/**
 * Trupp-adminens vy för sin trupp (§KM.3, `#193`, omgjord i `#279`, en trupp-bred roll i `#285`).
 *
 * <h3>En trupp-bred roll</h3>
 *
 * Rollen gäller hela truppen (P2016), inte en enskild färg. En admin för truppen skapar
 * färg-lagen, tilldelar barnen färger och sköter kallelser, inbjudningar och ansökningar.
 * (Koden kallar rollen "admin", §KM.9.) Gränssnittet sa tidigare "Tränare"; det är omdöpt till
 * "Administrera truppen" (#395) sedan färg-lag-tränaren blev en egen roll skild från trupp-admin
 * — annars går de två inte att skilja åt bredvid tränarens egen "Sköt laget".
 *
 * <h3>Översikt först, en sektion i taget</h3>
 *
 * Man landar på en lugn översikt och växlar mellan sektionerna med en flikrad — bara den
 * aktiva syns, så den gamla ändlösa kolumnen är borta.
 *
 * <h3>Synligheten är inte säkerheten</h3>
 *
 * Vyn göms för den som inte är tränare för någon trupp, men servern (policyn
 * <c>AdminOfTrupp</c>) är den riktiga grinden. Route-grinden kräver bara inloggning.
 */
export function AdminPage() {
  useDocumentTitle('Administrera truppen')
  const { status, isSuperAdmin, adminOf } = useAuth()
  const trupper = useMyTrupper()
  const [truppId, setTruppId] = useState<string | null>(null)

  if (status === 'okand') {
    return (
      <main className="page">
        <p className="state" role="status">
          Laddar…
        </p>
      </main>
    )
  }

  if (!isSuperAdmin && adminOf.length === 0) {
    return (
      <main className="page">
        <h1>Ingen behörighet</h1>
        <p className="state">Den här vyn är för truppens tränare.</p>
      </main>
    )
  }

  const options = trupper.data ?? []

  // Med bara en trupp finns inget att välja: den är förvald och väljaren visas inte. Väljaren
  // dyker upp först för den som är admin/tränare i flera trupper (t.ex. P2014 och P2016). Härlett
  // under render — inget eget tillstånd som kan hamna i otakt, och ingen blink från en effekt.
  const onlyTruppId = options.length === 1 ? options[0]!.id : null
  const selectedTruppId = truppId ?? onlyTruppId

  return (
    <main className="page admin">
      <header className="app-header">
        <h1>Administrera truppen</h1>
        <p>Hantera din trupp — skapa färg-lag, sortera barnen och bjud in vårdnadshavare.</p>
      </header>

      {trupper.isLoading && (
        <p className="state" role="status">
          Hämtar dina trupper…
        </p>
      )}
      {trupper.isError && (
        <p className="state state--error" role="alert">
          {trupper.error instanceof ApiError && trupper.error.offline
            ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
            : 'Kunde inte hämta dina trupper.'}
        </p>
      )}

      {trupper.data && options.length === 0 && (
        <p className="state">Du är inte admin för någon trupp än.</p>
      )}

      {options.length > 1 && (
        <div className="admin-trupp-picker form__field">
          <label htmlFor="admin-valj-trupp">Trupp</label>
          <select
            id="admin-valj-trupp"
            value={selectedTruppId ?? ''}
            onChange={(event) => setTruppId(event.target.value === '' ? null : event.target.value)}
          >
            <option value="">Välj trupp…</option>
            {options.map((trupp) => (
              <option key={trupp.id} value={trupp.id}>
                {trupp.clubName} · {trupp.name} {trupp.season}
              </option>
            ))}
          </select>
        </div>
      )}

      {selectedTruppId !== null && (
        <AdminSections key={selectedTruppId} truppId={selectedTruppId} />
      )}
    </main>
  )
}

/**
 * Flikraden och den aktiva sektionen. En riktig tablist för tangentbordet: piltangenter,
 * Home/End och `aria-selected`/`aria-controls`. Bara den valda panelen renderas.
 */
function AdminSections({ truppId }: { truppId: string }) {
  const [active, setActive] = useState<SectionKey>('oversikt')
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([])
  const baseId = useId()

  function moveTo(index: number): void {
    const key = SECTIONS[index]!
    setActive(key)
    tabRefs.current[index]?.focus()
  }

  function onKeyDown(event: React.KeyboardEvent, index: number): void {
    const last = SECTIONS.length - 1
    let next: number | null = null

    if (event.key === 'ArrowRight') next = index === last ? 0 : index + 1
    else if (event.key === 'ArrowLeft') next = index === 0 ? last : index - 1
    else if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = last

    if (next === null) {
      return
    }

    event.preventDefault()
    moveTo(next)
  }

  return (
    <>
      <div role="tablist" aria-label="Administrera truppen" className="admin-tabs">
        {SECTIONS.map((key, index) => (
          <button
            key={key}
            ref={(element) => {
              tabRefs.current[index] = element
            }}
            type="button"
            role="tab"
            id={`${baseId}-tab-${key}`}
            aria-selected={active === key}
            aria-controls={`${baseId}-panel-${key}`}
            tabIndex={active === key ? 0 : -1}
            className={
              active === key ? 'admin-tabs__tab admin-tabs__tab--active' : 'admin-tabs__tab'
            }
            onClick={() => setActive(key)}
            onKeyDown={(event) => onKeyDown(event, index)}
          >
            {LABEL[key]}
          </button>
        ))}
      </div>

      <div
        role="tabpanel"
        id={`${baseId}-panel-${active}`}
        aria-labelledby={`${baseId}-tab-${active}`}
        tabIndex={0}
        className="admin-panel"
      >
        {active === 'oversikt' && (
          <AdminOverview truppId={truppId} onGotoApplications={() => setActive('ansokningar')} />
        )}

        {active === 'aktiviteter' && <CreateActivity truppId={truppId} />}

        {active === 'barn' && <BarnOchLag truppId={truppId} />}

        {active === 'inbjudningar' && <InvitationsPanel truppId={truppId} />}

        {active === 'ansokningar' && <ApplicationsPanel truppId={truppId} />}

        {active === 'installningar' && <ClubVenueSettings truppId={truppId} />}
      </div>
    </>
  )
}
