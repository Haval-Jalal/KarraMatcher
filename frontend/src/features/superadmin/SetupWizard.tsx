import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { slugify } from '@/lib/slugify'

import { AdminStep } from './AdminStep'
import { superadminError } from './superadminError'
import {
  useClubs,
  useCreateClub,
  useCreateSport,
  useCreateTrupp,
  useSports,
  useTrupper,
} from './useSuperadmin'

/**
 * Superadmins uppsättningsguide (`#261`).
 *
 * <h3>En kedja att följa, inte fyra högar</h3>
 *
 * Superadmins jobb är en sekvens: skapa (eller välj) en <b>sport</b>, en <b>klubb</b>, en
 * <b>trupp</b>, och <b>tilldela en admin</b>. Sen tar truppens admin över och sköter lag,
 * barn och inbjudningar i sin egen vy. Guiden speglar den kedjan steg för steg i stället för
 * att lägga varje sak som en egen flik utan ordning.
 *
 * <h3>Välj eller skapa</h3>
 *
 * Vid varje steg kan man välja något som redan finns eller skapa nytt. Det löser dubbletterna
 * (man ser att Fotboll och Kärra redan finns och väljer dem i stället för att skapa en till).
 */

const STEPS = ['Sport', 'Klubb', 'Trupp', 'Tränare'] as const

export function SetupWizard() {
  const [step, setStep] = useState(0)
  const [sportId, setSportId] = useState<string | null>(null)
  const [clubId, setClubId] = useState<string | null>(null)
  const [truppId, setTruppId] = useState<string | null>(null)

  const sports = useSports()
  const clubs = useClubs()
  const trupper = useTrupper()
  const createSport = useCreateSport()
  const createClub = useCreateClub()

  const selectedSport = sports.data?.find((s) => s.id === sportId) ?? null
  const selectedClub = clubs.data?.find((c) => c.id === clubId) ?? null
  const selectedTrupp = trupper.data?.find((t) => t.id === truppId) ?? null

  const canAdvance = [sportId, clubId, truppId, null][step] !== null || step === 3

  function reset(): void {
    setStep(0)
    setSportId(null)
    setClubId(null)
    setTruppId(null)
  }

  return (
    <div className="wizard">
      <ol className="wizard__steps">
        {STEPS.map((label, index) => (
          <li
            key={label}
            className={
              index === step
                ? 'wizard__step wizard__step--current'
                : index < step
                  ? 'wizard__step wizard__step--done'
                  : 'wizard__step'
            }
          >
            <button
              type="button"
              className="wizard__step-button"
              // Bara klara steg går att hoppa tillbaka till; framåt sker via Nästa.
              disabled={index > step}
              aria-current={index === step ? 'step' : undefined}
              onClick={() => {
                setStep(index)
              }}
            >
              <span className="wizard__step-number" aria-hidden="true">
                {index + 1}
              </span>{' '}
              {label}
            </button>
          </li>
        ))}
      </ol>

      {step === 0 && (
        <PickOrCreate
          heading="Steg 1 av 4 — Sport"
          singular="sport"
          items={sports.data ?? []}
          isLoading={sports.isLoading}
          selectedId={sportId}
          onSelect={setSportId}
          onCreate={createSportAndSelect}
        />
      )}

      {step === 1 && (
        <PickOrCreate
          heading="Steg 2 av 4 — Klubb"
          singular="klubb"
          items={clubs.data ?? []}
          isLoading={clubs.isLoading}
          selectedId={clubId}
          onSelect={setClubId}
          onCreate={createClubAndSelect}
        />
      )}

      {step === 2 && selectedClub !== null && selectedSport !== null && (
        <TruppStep
          clubId={selectedClub.id}
          clubName={selectedClub.name}
          sportId={selectedSport.id}
          sportName={selectedSport.name}
          trupper={(trupper.data ?? []).filter((t) => t.clubId === selectedClub.id)}
          isLoading={trupper.isLoading}
          selectedId={truppId}
          onSelect={setTruppId}
        />
      )}

      {step === 3 && selectedTrupp !== null && (
        <div className="wizard__panel">
          <h3>Steg 4 av 4 — Tilldela tränare</h3>
          <p className="wizard__context">
            {selectedTrupp.clubName} · {selectedTrupp.name}
          </p>
          <p className="state">
            Tilldela en eller flera tränare. Sedan sköter de färg-lagen, barnen och inbjudningarna i
            truppen — du är klar här.
          </p>
          <AdminStep truppId={selectedTrupp.id} />
        </div>
      )}

      <div className="wizard__nav">
        <button
          type="button"
          className="button"
          disabled={step === 0}
          onClick={() => {
            setStep((current) => Math.max(0, current - 1))
          }}
        >
          Tillbaka
        </button>

        {step < 3 ? (
          <button
            type="button"
            className="button"
            disabled={!canAdvance}
            onClick={() => {
              setStep((current) => current + 1)
            }}
          >
            Nästa
          </button>
        ) : (
          <button type="button" className="button" onClick={reset}>
            Klar — sätt upp en till
          </button>
        )}
      </div>
    </div>
  )

  // Skapar och väljer i ett svep, så nästa steg blir aktiverat direkt.
  async function createSportAndSelect(name: string, slug: string): Promise<void> {
    const created = await createSport.mutateAsync({ name, slug })
    setSportId(created.id)
  }

  async function createClubAndSelect(name: string, slug: string): Promise<void> {
    const created = await createClub.mutateAsync({ name, slug })
    setClubId(created.id)
  }
}

const slugSchema = z.object({
  name: z.string().trim().min(1, 'Fyll i namnet.'),
  slug: z
    .string()
    .trim()
    .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, 'Bara små bokstäver a–z, siffror och bindestreck.'),
})

type SlugValues = z.infer<typeof slugSchema>

/** Steg för sport/klubb: välj en befintlig eller skapa ny (med slug-förslag). */
function PickOrCreate({
  heading,
  singular,
  items,
  isLoading,
  selectedId,
  onSelect,
  onCreate,
}: {
  heading: string
  singular: string
  items: { id: string; name: string; slug: string }[]
  isLoading: boolean
  selectedId: string | null
  onSelect: (id: string) => void
  onCreate: (name: string, slug: string) => Promise<void>
}) {
  const [adding, setAdding] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [slugEdited, setSlugEdited] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<SlugValues>({
    resolver: zodResolver(slugSchema),
    defaultValues: { name: '', slug: '' },
  })

  const nameField = register('name')
  const slugField = register('slug')

  useEffect(() => {
    if (adding) {
      setFocus('name')
    }
  }, [adding, setFocus])

  return (
    <div className="wizard__panel">
      <h3>{heading}</h3>
      <p className="state">Välj en {singular} som redan finns, eller skapa en ny.</p>

      {isLoading && <p className="state">Hämtar…</p>}

      {items.length > 0 && (
        <ul className="wizard__choices" role="radiogroup" aria-label={`Välj ${singular}`}>
          {items.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                role="radio"
                aria-checked={selectedId === item.id}
                className={
                  selectedId === item.id
                    ? 'wizard__choice wizard__choice--selected'
                    : 'wizard__choice'
                }
                onClick={() => {
                  onSelect(item.id)
                }}
              >
                <strong>{item.name}</strong> <code>{item.slug}</code>
              </button>
            </li>
          ))}
        </ul>
      )}

      {!adding ? (
        <div className="actions">
          <button
            type="button"
            className="button button--action"
            onClick={() => {
              setAdding(true)
            }}
          >
            {`Skapa ny ${singular}`}
          </button>
        </div>
      ) : (
        <form
          className="form"
          noValidate
          onSubmit={(event) => {
            void handleSubmit(async (values) => {
              try {
                await onCreate(values.name.trim(), values.slug.trim())
                reset()
                setSlugEdited(false)
                setAdding(false)
                setFailure(null)
              } catch (error) {
                setFailure(superadminError(error))
              }
            })(event)
          }}
        >
          <div className="form__field">
            <label htmlFor={`ny-${singular}-namn`}>Namn</label>
            <input
              id={`ny-${singular}-namn`}
              type="text"
              autoComplete="off"
              aria-invalid={errors.name ? true : undefined}
              {...nameField}
              onChange={(event) => {
                void nameField.onChange(event)
                if (!slugEdited) {
                  setValue('slug', slugify(event.target.value))
                }
              }}
            />
            {errors.name && <p className="form__error">{errors.name.message}</p>}
          </div>

          <div className="form__field">
            <label htmlFor={`ny-${singular}-slug`}>Slug (i länkar, ändras inte sedan)</label>
            <input
              id={`ny-${singular}-slug`}
              type="text"
              autoComplete="off"
              aria-invalid={errors.slug ? true : undefined}
              {...slugField}
              onChange={(event) => {
                void slugField.onChange(event)
                setSlugEdited(true)
              }}
            />
            {errors.slug && <p className="form__error">{errors.slug.message}</p>}
          </div>

          {failure !== null && (
            <p className="state state--error" role="alert">
              {failure}
            </p>
          )}

          <div className="actions">
            <button type="submit" className="button" disabled={isSubmitting}>
              {isSubmitting ? 'Skapar…' : `Skapa ${singular}`}
            </button>
            <button
              type="button"
              className="button"
              onClick={() => {
                setAdding(false)
                setFailure(null)
                setSlugEdited(false)
                reset()
              }}
            >
              Avbryt
            </button>
          </div>
        </form>
      )}
    </div>
  )
}

const truppSchema = z.object({ name: z.string().trim().min(1, 'Fyll i truppens namn.') })

/** Trupp-steget: skapa en trupp under vald klubb + sport, eller välj en befintlig. */
function TruppStep({
  clubId,
  clubName,
  sportId,
  sportName,
  trupper,
  isLoading,
  selectedId,
  onSelect,
}: {
  clubId: string
  clubName: string
  sportId: string
  sportName: string
  trupper: { id: string; name: string }[]
  isLoading: boolean
  selectedId: string | null
  onSelect: (id: string) => void
}) {
  const create = useCreateTrupp()
  const [adding, setAdding] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<{ name: string }>({ resolver: zodResolver(truppSchema), defaultValues: { name: '' } })

  useEffect(() => {
    if (adding) {
      setFocus('name')
    }
  }, [adding, setFocus])

  return (
    <div className="wizard__panel">
      <h3>Steg 3 av 4 — Trupp</h3>
      <p className="wizard__context">
        {clubName} · {sportName}
      </p>
      <p className="state">Skapa en trupp under klubben, eller välj en som redan finns.</p>

      {isLoading && <p className="state">Hämtar…</p>}

      {trupper.length > 0 && (
        <ul className="wizard__choices" role="radiogroup" aria-label="Välj trupp">
          {trupper.map((trupp) => (
            <li key={trupp.id}>
              <button
                type="button"
                role="radio"
                aria-checked={selectedId === trupp.id}
                className={
                  selectedId === trupp.id
                    ? 'wizard__choice wizard__choice--selected'
                    : 'wizard__choice'
                }
                onClick={() => {
                  onSelect(trupp.id)
                }}
              >
                <strong>{trupp.name}</strong>
              </button>
            </li>
          ))}
        </ul>
      )}

      {!adding ? (
        <div className="actions">
          <button
            type="button"
            className="button button--action"
            onClick={() => {
              setAdding(true)
            }}
          >
            Skapa ny trupp
          </button>
        </div>
      ) : (
        <form
          className="form"
          noValidate
          onSubmit={(event) => {
            void handleSubmit(async (values) => {
              try {
                const created = await create.mutateAsync({
                  clubId,
                  sportId,
                  name: values.name.trim(),
                })
                reset()
                setAdding(false)
                setFailure(null)
                onSelect(created.id)
              } catch (error) {
                setFailure(superadminError(error))
              }
            })(event)
          }}
        >
          <div className="form__field">
            <label htmlFor="ny-trupp-namn">Namn (t.ex. P2016)</label>
            <input id="ny-trupp-namn" type="text" autoComplete="off" {...register('name')} />
            {errors.name && <p className="form__error">{errors.name.message}</p>}
          </div>

          {failure !== null && (
            <p className="state state--error" role="alert">
              {failure}
            </p>
          )}

          <div className="actions">
            <button type="submit" className="button" disabled={isSubmitting}>
              {isSubmitting ? 'Skapar…' : 'Skapa trupp'}
            </button>
            <button
              type="button"
              className="button"
              onClick={() => {
                setAdding(false)
                setFailure(null)
                reset()
              }}
            >
              Avbryt
            </button>
          </div>
        </form>
      )}
    </div>
  )
}
