import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { useTeams } from '@/features/teams'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { BADGES, earnedBadges, totalsFor } from './badges/badges'
import { InstallTip } from './storage/InstallTip'
import { StorageNotice } from './storage/StorageNotice'
import type { Child } from './storage/schema'
import { BackupSection } from './BackupSection'
import { usePlayerCard } from './usePlayerCard'

/**
 * Barnen på den här telefonen.
 *
 * <h3>Vad den ska kännas som</h3>
 *
 * Något man gör hemma vid köksbordet på tio sekunder. Ingen inloggning, ingen server, och
 * ingen validering som kräver ett riktigt namn — <b>smeknamn duger</b>. Den som skriver
 * "Lillen" ska inte mötas av ett felmeddelande om att namnet ser fel ut.
 *
 * <h3>Varför texten säger var datan finns</h3>
 *
 * Kortet lämnar aldrig telefonen (§KM.2), vilket är en styrka men också en risk: byts
 * telefonen utan säkerhetskopia är säsongen borta. Att vara tydlig med det är inte en
 * brasklapp utan en förutsättning för att någon ska kunna fatta ett informerat beslut om
 * att säkerhetskopiera.
 */

const schema = z.object({
  // Bara ett tecken kravs. Kravet finns for att skilja "inget ifyllt" fran ett namn,
  // inte for att doma om vad ett namn ar.
  name: z
    .string()
    .trim()
    .min(1, 'Skriv vad barnet ska heta i appen.')
    .max(40, 'Namnet är för långt.'),
  shirtNumber: z.string().trim().max(3, 'Tröjnumret är för långt.'),
  teamSlug: z.string(),
})

type FormValues = z.infer<typeof schema>

export function ChildrenPage() {
  const { card, addChild, updateChild, removeChild, reload } = usePlayerCard()
  const { data: teams } = useTeams()
  const [editing, setEditing] = useState<Child | null>(null)
  const [confirmRemove, setConfirmRemove] = useState<Child | null>(null)
  const [failure, setFailure] = useState<string | null>(null)

  useDocumentTitle('Spelarkortet')

  const report = (saved: boolean) => {
    setFailure(saved ? null : 'Det gick inte att spara. Telefonens lagring kan vara full.')
  }

  return (
    <main>
      <header className="app-header">
        <h1>Spelarkortet</h1>
        <p className="app-header__subtitle">
          Barnens matcher och statistik, bara på den här telefonen.
        </p>
      </header>

      <StorageNotice />

      <InstallTip hasContent={card.children.length > 0 || card.reports.length > 0} />

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      <h2>Barn</h2>

      {card.children.length === 0 ? (
        <p className="state">Inga barn tillagda än. Lägg till det första nedan.</p>
      ) : (
        <ul className="children">
          {card.children.map((child) => (
            <li key={child.id} className="children__row">
              <span className="children__name">{child.name}</span>
              {child.shirtNumber !== null && child.shirtNumber !== '' && (
                <span className="children__number">{`Nr ${child.shirtNumber}`}</span>
              )}
              <span className="children__team">
                {teams?.find((team) => team.slug === child.teamSlug)?.name ?? 'Inget lag valt'}
              </span>

              <button
                type="button"
                className="button"
                onClick={() => {
                  setEditing(child)
                }}
              >
                <span aria-hidden="true">Ändra</span>
                <span className="visually-hidden">{`Ändra ${child.name}`}</span>
              </button>

              <button
                type="button"
                className="button button--danger"
                onClick={() => {
                  setConfirmRemove(child)
                }}
              >
                <span aria-hidden="true">Ta bort</span>
                <span className="visually-hidden">{`Ta bort ${child.name}`}</span>
              </button>

              {/*
                Marken visas inte har langre utan pa barnets egen sida (#46), dar de star
                bredvid siffrorna de raknas ur. Raden sager anda hur manga som ar upplasta,
                sa man ser att det finns nagot att oppna.
              */}
              <Link
                className="button children__card"
                to="/spelarkort/$childId"
                params={{ childId: child.id }}
                aria-label={`Öppna spelarkortet för ${child.name} — ${String(earnedBadges(totalsFor(card, child.id)).length)} av ${String(BADGES.length)} märken upplåsta`}
              >
                {`Spelarkort — ${String(earnedBadges(totalsFor(card, child.id)).length)} av ${String(BADGES.length)} märken`}
              </Link>
            </li>
          ))}
        </ul>
      )}

      <ChildForm
        key={editing?.id ?? 'nytt'}
        existing={editing}
        teams={teams ?? []}
        onSubmit={(values) => {
          const saved =
            editing === null
              ? addChild({
                  name: values.name.trim(),
                  shirtNumber: blank(values.shirtNumber),
                  teamSlug: blank(values.teamSlug),
                })
              : updateChild(editing.id, {
                  name: values.name.trim(),
                  shirtNumber: blank(values.shirtNumber),
                  teamSlug: blank(values.teamSlug),
                })

          report(saved)
          setEditing(null)
        }}
        onCancel={
          editing === null
            ? null
            : () => {
                setEditing(null)
              }
        }
      />

      <BackupSection onChanged={reload} />

      {confirmRemove !== null && (
        <section className="danger-zone">
          <h2>{`Ta bort ${confirmRemove.name}?`}</h2>

          <p className="state" role="alert">
            <strong>Barnets matchrapporter tas bort med.</strong> Det går inte att ångra, och finns
            ingen kopia på servern — statistiken finns bara här.
          </p>

          <div className="actions">
            <button
              type="button"
              className="button"
              onClick={() => {
                setConfirmRemove(null)
              }}
            >
              Avbryt
            </button>
            <button
              type="button"
              className="button button--danger"
              onClick={() => {
                report(removeChild(confirmRemove.id))
                setConfirmRemove(null)
              }}
            >
              {`Ja, ta bort ${confirmRemove.name}`}
            </button>
          </div>
        </section>
      )}
    </main>
  )
}

function ChildForm({
  existing,
  teams,
  onSubmit,
  onCancel,
}: {
  existing: Child | null
  teams: { slug: string; name: string }[]
  onSubmit: (values: FormValues) => void
  onCancel: (() => void) | null
}) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: existing?.name ?? '',
      shirtNumber: existing?.shirtNumber ?? '',
      teamSlug: existing?.teamSlug ?? '',
    },
  })

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(onSubmit)(event)
      }}
    >
      <h2>{existing === null ? 'Lägg till barn' : `Ändra ${existing.name}`}</h2>

      <div className="form__field">
        <label htmlFor="barnnamn">Namn eller smeknamn</label>
        <input
          id="barnnamn"
          type="text"
          autoComplete="off"
          aria-describedby={errors.name ? 'barnnamn-fel' : undefined}
          aria-invalid={errors.name ? true : undefined}
          {...register('name')}
        />
        {errors.name && (
          <p className="form__error" id="barnnamn-fel">
            {errors.name.message}
          </p>
        )}
      </div>

      <div className="form__field">
        <label htmlFor="trojnummer">Tröjnummer (valfritt)</label>
        <input
          id="trojnummer"
          type="text"
          inputMode="numeric"
          autoComplete="off"
          {...register('shirtNumber')}
        />
      </div>

      <div className="form__field">
        <label htmlFor="barnlag">Lag (valfritt)</label>
        <select id="barnlag" {...register('teamSlug')}>
          <option value="">Inget lag valt</option>
          {teams.map((team) => (
            <option key={team.slug} value={team.slug}>
              {team.name}
            </option>
          ))}
        </select>
      </div>

      <div className="actions">
        <button type="submit" className="button">
          {existing === null ? 'Lägg till' : 'Spara'}
        </button>
        {onCancel !== null && (
          <button type="button" className="button" onClick={onCancel}>
            Avbryt
          </button>
        )}
      </div>
    </form>
  )
}

/** Tom text betyder "inget värde", inte ett värde som är tomt. */
function blank(value: string): string | null {
  return value.trim() === '' ? null : value.trim()
}
