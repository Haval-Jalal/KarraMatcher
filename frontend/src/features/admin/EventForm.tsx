import { zodResolver } from '@hookform/resolvers/zod'
import { type ReactNode, useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { useClubVenue } from '@/features/clubs/useClubVenue'
import { type TeamEvent, venueLine } from '@/features/events'
import { ApiError } from '@/lib/api'
import { swedishLocalToUtc, utcToSwedishLocalInput } from '@/lib/time'

import type { EventInput } from './adminApi'
import { useAddressSuggestions } from './useAddressSuggestions'

/**
 * Tränarens formulär för en händelse — match, träning, cup eller övrigt (`#198`, `#307`).
 *
 * <h3>Typen styr fälten</h3>
 *
 * En match har motståndare; en träning/cup/övrig händelse en rubrik. Väljaren högst upp
 * bestämmer vilka fält som visas.
 *
 * <h3>Plats: hemma eller annan plats</h3>
 *
 * <b>Hemma</b> använder klubbens hemmaplan (adressen fylls i automatiskt server-side). <b>Annan
 * plats</b> — en bortamatch eller t.ex. en vinterträning inomhus — låter tränaren skriva
 * adressen, som geokodas. Gäller alla typer (`#307`).
 *
 * <h3>Tiden skrivs i svensk tid och sparas i UTC</h3>
 *
 * Omräkningen sker i `lib/time.ts`, frontendens enda ställe där UTC möter svensk tid (§KM.5).
 */

/** Taket för notisen, delat mellan zod-schemat och textrutans räknare. Speglar backend. */
const NOTE_MAX_LENGTH = 500

const schema = z
  .object({
    type: z.enum(['Match', 'Training', 'Other', 'Cup']),
    kickoffLocal: z
      .string()
      .min(1, 'Fyll i datum och tid.')
      .refine((value) => swedishLocalToUtc(value) !== null, 'Datum och tid ser inte riktiga ut.'),
    opponent: z.string().max(120, 'Namnet är för långt.'),
    isHome: z.boolean(),
    address: z.string().max(200, 'Adressen är för lång.'),
    title: z.string().max(120, 'Rubriken är för lång.'),
    note: z.string().max(NOTE_MAX_LENGTH, 'Notisen är för lång.'),
  })
  .superRefine((values, ctx) => {
    if (values.type === 'Match') {
      if (values.opponent.trim() === '') {
        ctx.addIssue({ path: ['opponent'], code: 'custom', message: 'Fyll i motståndarlaget.' })
      }
    } else if (values.title.trim() === '') {
      ctx.addIssue({ path: ['title'], code: 'custom', message: 'Fyll i en rubrik.' })
    }

    if (!values.isHome && values.address.trim() === '') {
      ctx.addIssue({ path: ['address'], code: 'custom', message: 'Skriv adressen till platsen.' })
    }
  })

type FormValues = z.infer<typeof schema>

const TYPE_LABELS: Record<FormValues['type'], string> = {
  Match: 'Match',
  Training: 'Träning',
  Cup: 'Cup',
  Other: 'Övrigt',
}

export function EventForm({
  truppId,
  existing,
  onSubmit,
  onCancel,
  onTypeChange,
  targetSlot,
  autoFocus = false,
}: {
  truppId: string
  existing?: TeamEvent
  onSubmit: (input: EventInput) => Promise<void>
  onCancel: () => void
  /**
   * Flytta fokus till första fältet när formuläret monteras. Sätts där formuläret fälls in i
   * stället för en knapp (tränarens/adminens "Lägg till händelse"), så fokus inte faller till
   * <body> (WCAG 2.4.3, #598). Utelämnas där formuläret alltid visas (CreateActivity).
   */
  autoFocus?: boolean
  /**
   * Meddelar föräldern när typen ändras (`#333`), så en admin-vy kan byta kallelsens förval per
   * typ. Utelämnas i tränarens lag-väg — där finns ingen målgrupp.
   */
  onTypeChange?: (type: EventInput['type']) => void
  /** Valfritt innehåll (t.ex. kallelse-målgruppen) som visas inuti formuläret, före knapparna. */
  targetSlot?: ReactNode
}) {
  const [failure, setFailure] = useState<string | null>(null)
  const club = useClubVenue(truppId)

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      type: existing?.type ?? 'Match',
      kickoffLocal: existing ? utcToSwedishLocalInput(existing.kickoffUtc) : '',
      opponent: existing?.opponent ?? '',
      isHome: existing?.isHome ?? true,
      address: existing && existing.isHome === false ? existing.address : '',
      title: existing?.title ?? '',
      // Förifyll notisen vid redigering (`#468`) — annars skickades ett tomt fält som nollade
      // den lagrade notisen.
      note: existing?.note ?? '',
    },
  })

  // Typen och hemma/borta styr vilka fält som visas. Hålls i lokal state (React Compiler kan
  // inte memoisera RHF:s watch() säkert); setValue håller formulärvärdet i takt.
  const [type, setType] = useState<FormValues['type']>(existing?.type ?? 'Match')
  const [isHome, setIsHome] = useState(existing?.isHome ?? true)
  const isMatch = type === 'Match'

  // Adress-förslag för "annan plats" (`#307`). Speglar fältets text; tystas när platsen är hemma
  // (tom term → inget anrop). Servern geokodar ändå det valda vid spar, aldrig klienten.
  const [addressText, setAddressText] = useState(
    existing && existing.isHome === false ? existing.address : '',
  )
  const addressField = register('address')
  const addressSuggestions = useAddressSuggestions(isHome ? '' : addressText)

  // Teckenräknare för notisen (`#609`). Lokal längd i stället för watch(): en räknare behöver bara
  // ett tal, och watch() kan inte memoiseras säkert av React Compiler (samma skäl som type/isHome).
  const noteField = register('note')
  const [noteLength, setNoteLength] = useState(existing?.note?.length ?? 0)

  // Fälls formuläret in i stället för en knapp flyttas fokus till dess första kontroll (#598).
  const formRef = useRef<HTMLFormElement>(null)
  useEffect(() => {
    if (autoFocus) {
      formRef.current?.querySelector<HTMLElement>('input, select, textarea, button')?.focus()
    }
  }, [autoFocus])

  return (
    <form
      ref={formRef}
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async (values) => {
          const kickoffUtc = swedishLocalToUtc(values.kickoffLocal)

          if (kickoffUtc === null) {
            setFailure('Datum och tid ser inte riktiga ut.')

            return
          }

          const matchType = values.type === 'Match'

          try {
            await onSubmit({
              type: values.type,
              kickoffUtc,
              title: matchType ? null : values.title.trim(),
              opponent: matchType ? values.opponent.trim() : null,
              isHome: values.isHome,
              address: values.isHome ? null : values.address.trim(),
              note: values.note.trim() === '' ? null : values.note.trim(),
            })
            setFailure(null)
          } catch (error) {
            setFailure(
              error instanceof ApiError && !error.offline
                ? error.message
                : 'Ingen anslutning. Kontrollera nätet och försök igen.',
            )
          }
        })(event)
      }}
    >
      <fieldset className="form__field">
        <legend>Typ</legend>
        {existing !== undefined ? (
          // Typen är oföränderlig vid redigering (servern ignorerar ett byte, `#466`) — visa den
          // som läsvärde i stället för en väljare som tyst skulle nolla motståndare/rubrik.
          <p className="state">{TYPE_LABELS[type]}</p>
        ) : (
          <div className="seg">
            {(['Match', 'Training', 'Cup', 'Other'] as const).map((value) => (
              <label key={value} className="seg__option">
                <input
                  type="radio"
                  name="handelsetyp"
                  className="visually-hidden"
                  checked={type === value}
                  onChange={() => {
                    setType(value)
                    setValue('type', value, { shouldValidate: false })
                    onTypeChange?.(value)
                  }}
                />
                <span>{TYPE_LABELS[value]}</span>
              </label>
            ))}
          </div>
        )}
      </fieldset>

      <div className="form__field">
        <label htmlFor="avspark">{isMatch ? 'Avspark (svensk tid)' : 'Start (svensk tid)'}</label>
        <input
          id="avspark"
          type="datetime-local"
          aria-describedby={errors.kickoffLocal ? 'avspark-fel' : undefined}
          aria-invalid={errors.kickoffLocal ? true : undefined}
          {...register('kickoffLocal')}
        />
        {errors.kickoffLocal && (
          <p className="form__error" id="avspark-fel">
            {errors.kickoffLocal.message}
          </p>
        )}
      </div>

      {isMatch ? (
        <div className="form__field">
          <label htmlFor="motstandare">Motståndare</label>
          <input
            id="motstandare"
            type="text"
            autoComplete="off"
            aria-describedby={errors.opponent ? 'motstandare-fel' : undefined}
            aria-invalid={errors.opponent ? true : undefined}
            {...register('opponent')}
          />
          {errors.opponent && (
            <p className="form__error" id="motstandare-fel">
              {errors.opponent.message}
            </p>
          )}
        </div>
      ) : (
        <div className="form__field">
          <label htmlFor="rubrik">Rubrik</label>
          <input
            id="rubrik"
            type="text"
            autoComplete="off"
            aria-describedby={errors.title ? 'rubrik-fel' : undefined}
            aria-invalid={errors.title ? true : undefined}
            {...register('title')}
          />
          {errors.title && (
            <p className="form__error" id="rubrik-fel">
              {errors.title.message}
            </p>
          )}
        </div>
      )}

      {/* Plats: hemma (klubbens plan) eller annan plats (skriven adress, geokodas) (`#307`). */}
      <fieldset className="form__field">
        <legend>Plats</legend>

        <label className="opt">
          <input
            type="radio"
            name="plats"
            checked={isHome}
            onChange={() => {
              setIsHome(true)
              setValue('isHome', true, { shouldValidate: true })
            }}
          />
          <span className="opt__txt">
            {isMatch ? 'Hemmamatch' : 'Hemma'}
            <small>Truppens hemmaplan</small>
          </span>
        </label>

        <label className="opt">
          <input
            type="radio"
            name="plats"
            checked={!isHome}
            onChange={() => {
              setIsHome(false)
              setValue('isHome', false, { shouldValidate: true })
            }}
          />
          <span className="opt__txt">
            {isMatch ? 'Bortamatch' : 'Annan plats'}
            <small>Skriv adressen</small>
          </span>
        </label>

        {isHome ? (
          club.isPending ? (
            // Under Renders kallstart (~50 s, §KM.11) hänger venue-frågan. Visa ett neutralt
            // laddningsbesked i stället för det röda "ingen hemmaplan", som annars blinkade förbi
            // för trupper som visst har en plan (`#478`).
            <p className="state" role="status">
              Hämtar hemmaplan…
            </p>
          ) : club.data?.configured ? (
            <p className="state">
              Truppens hemmaplan: {venueLine(club.data.name ?? '', club.data.address ?? '')}
            </p>
          ) : club.data ? (
            <p className="state state--error" role="alert">
              Truppen har ingen hemmaplan ännu. Sätt den under Inställningar innan du lägger upp en
              hemma-aktivitet.
            </p>
          ) : (
            // Frågan gick inte igenom (offline/fel) — säg det, i stället för att påstå att planen
            // saknas (`#478`).
            <p className="state state--error" role="alert">
              Kunde inte hämta hemmaplanen. Kontrollera nätet och försök igen.
            </p>
          )
        ) : (
          <div className="form__field">
            <label htmlFor="adress">Adress</label>
            <input
              id="adress"
              type="text"
              autoComplete="off"
              list="adress-forslag"
              placeholder="t.ex. Bortavägen 5, Kungälv"
              aria-describedby={errors.address ? 'adress-fel' : undefined}
              aria-invalid={errors.address ? true : undefined}
              {...addressField}
              onChange={(event) => {
                void addressField.onChange(event)
                setAddressText(event.target.value)
              }}
            />
            {/* Förslagen medan man skriver (`#307`). Native datalist: tangentbord och skärmläsare
                får den gratis, och den som är offline ser bara inga förslag — fältet är fritext. */}
            <datalist id="adress-forslag">
              {addressSuggestions.map((suggestion) => (
                <option key={suggestion} value={suggestion} />
              ))}
            </datalist>
            <p className="admin-muted">
              Börja skriva så föreslås adresser. Rätt plats hittas när du sparar.
            </p>
            {errors.address && (
              <p className="form__error" id="adress-fel">
                {errors.address.message}
              </p>
            )}
          </div>
        )}
      </fieldset>

      <div className="form__field">
        <label htmlFor="notis">Notis till föräldrarna (valfritt)</label>
        <textarea
          id="notis"
          rows={3}
          maxLength={NOTE_MAX_LENGTH}
          aria-describedby="notis-rakna"
          {...noteField}
          onChange={(event) => {
            void noteField.onChange(event)
            setNoteLength(event.target.value.length)
          }}
        />
        <p className="form__count" id="notis-rakna" aria-live="polite">
          {noteLength}/{NOTE_MAX_LENGTH} tecken
        </p>
      </div>

      {targetSlot}

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      <div className="actions">
        <button type="submit" className="button button--action" disabled={isSubmitting}>
          {isSubmitting ? 'Sparar…' : existing ? 'Spara ändringen' : 'Lägg till händelsen'}
        </button>
        <button type="button" className="button" onClick={onCancel}>
          Avbryt
        </button>
      </div>
    </form>
  )
}
