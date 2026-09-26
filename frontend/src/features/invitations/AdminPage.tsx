import { useId, useRef, useState } from 'react'

import { ApplicationsPanel } from '@/features/applications'
import { useAuth } from '@/features/auth'
import { BarnOchLag } from '@/features/children'
import { ClubVenueSettings } from '@/features/clubs'

import { AdminOverview } from './AdminOverview'
import { InvitationsPanel } from './InvitationsPanel'
import { useMyTrupper } from './useInvitations'

const SECTIONS = ['oversikt', 'barn', 'inbjudningar', 'ansokningar', 'installningar'] as const
type SectionKey = (typeof SECTIONS)[number]

const LABEL: Record<SectionKey, string> = {
  oversikt: 'Översikt',
  barn: 'Barn & lag',
  inbjudningar: 'Inbjudningar',
  ansokningar: 'Ansökningar',
  installningar: 'Inställningar',
}

/**
 * Tränarens vy för sin trupp (§KM.3, `#193`, omgjord i `#279`, en trupp-bred tränarroll i `#285`).
 *
 * <h3>En trupp-bred roll</h3>
 *
 * Tränare gäller hela truppen (P2016), inte en enskild färg. En tränare skapar färg-lagen,
 * tilldelar barnen färger och sköter kallelser, inbjudningar och ansökningar. (Koden kallar
 * rollen fortfarande "admin", §KM.9 — bara gränssnittet säger "Tränare".)
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
  const { status, isSuperAdmin, adminOf } = useAuth()
  const trupper = useMyTrupper()
  const [truppId, setTruppId] = useState<string | null>(null)

  if (status === 'okand') {
    return (
      <main className="page">
        <p className="state">Laddar…</p>
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

  return (
    <main className="page admin">
      <header className="app-header">
        <h1>Tränare</h1>
        <p>Hantera din trupp — skapa färg-lag, sortera barnen och bjud in vårdnadshavare.</p>
      </header>

      {trupper.isLoading && <p className="state">Hämtar dina trupper…</p>}
      {trupper.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta dina trupper.
        </p>
      )}

      {trupper.data && options.length === 0 && (
        <p className="state">Du är inte admin för någon trupp än.</p>
      )}

      {options.length > 0 && (
        <div className="admin-trupp-picker form__field">
          <label htmlFor="admin-valj-trupp">Trupp</label>
          <select
            id="admin-valj-trupp"
            value={truppId ?? ''}
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

      {truppId !== null && <AdminSections key={truppId} truppId={truppId} />}
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

        {active === 'barn' && <BarnOchLag truppId={truppId} />}

        {active === 'inbjudningar' && <InvitationsPanel truppId={truppId} />}

        {active === 'ansokningar' && <ApplicationsPanel truppId={truppId} />}

        {active === 'installningar' && <ClubVenueSettings truppId={truppId} />}
      </div>
    </>
  )
}
