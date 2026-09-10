import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'
import { swedishLocalToUtc, utcToSwedishLocalInput } from '@/lib/time'

import {
  MAX_MESSAGE_LENGTH,
  MAX_SEATS,
  type CarpoolDirection,
  type CarpoolOfferInput,
} from './carpoolApi'

/**
 * Att lägga upp en skjuts.
 *
 * <h3>Kravet som formar formuläret</h3>
 *
 * "Under 30 sekunder från mobilen" (`#53`). Därför är avgångstiden redan ifylld — tre
 * kvart före avspark, som är när man faktiskt åker — riktningen förvald till matchen, och
 * notisen valfri. Det som återstår att skriva är var man åker ifrån.
 *
 * <h3>Tiden skrivs i svensk tid och skickas i UTC</h3>
 *
 * Omräkningen sker i `lib/time.ts` (§KM.5). En lokal tid som skickas rakt av blir två
 * timmar fel på sommaren, och då står någon på fel plats vid fel klockslag.
 */

const schema = z.object({
  direction: z.enum(['ToMatch', 'FromMatch', 'Both']),
  departurePlace: z
    .string()
    .trim()
    .min(1, 'Skriv var ni åker ifrån.')
    .max(120, 'Avgångsplatsen är för lång.'),
  departureLocal: z
    .string()
    .min(1, 'Fyll i avgångstid.')
    .refine((value) => swedishLocalToUtc(value) !== null, 'Tiden ser inte riktig ut.'),
  seats: z.number().int().min(1).max(MAX_SEATS),
  note: z.string().max(MAX_MESSAGE_LENGTH, 'Notisen är för lång.'),
})

type FormValues = z.infer<typeof schema>

/** Tre kvart före avspark — när man lämnar hemmet, inte när matchen börjar. */
const MINUTES_BEFORE_KICKOFF = 45

function defaultDeparture(kickoffUtc: string): string {
  const kickoff = new Date(kickoffUtc)

  return utcToSwedishLocalInput(new Date(kickoff.getTime() - MINUTES_BEFORE_KICKOFF * 60 * 1000))
}

const DIRECTIONS: { value: CarpoolDirection; label: string }[] = [
  { value: 'ToMatch', label: 'Till matchen' },
  { value: 'FromMatch', label: 'Hem från matchen' },
  { value: 'Both', label: 'Båda hållen' },
]

export function CarpoolOfferForm({
  kickoffUtc,
  onSubmit,
  onCancel,
}: {
  kickoffUtc: string
  onSubmit: (input: CarpoolOfferInput) => Promise<void>
  onCancel: () => void
}) {
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      direction: 'ToMatch',
      departurePlace: '',
      departureLocal: defaultDeparture(kickoffUtc),
      seats: 1,
      note: '',
    },
  })

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async (values) => {
          const departureUtc = swedishLocalToUtc(values.departureLocal)

          if (departureUtc === null) {
            setFailure('Tiden ser inte riktig ut.')

            return
          }

          try {
            await onSubmit({
              direction: values.direction,
              departurePlace: values.departurePlace.trim(),
              departureUtc,
              seats: values.seats,
              note: values.note.trim() === '' ? null : values.note.trim(),
            })
            setFailure(null)
          } catch (error) {
            setFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Erbjudandet är inte sparat — försök igen när du har nät.'
                : 'Erbjudandet gick inte att spara just nu. Försök igen om en stund.',
            )
          }
        })(event)
      }}
    >
      <fieldset className="form__field">
        <legend>Vilket håll kör du?</legend>
        {DIRECTIONS.map(({ value, label }) => (
          <label key={value} className="form__choice">
            <input type="radio" value={value} {...register('direction')} />
            {label}
          </label>
        ))}
      </fieldset>

      <div className="form__field">
        <label htmlFor="avgangsplats">Var åker ni ifrån?</label>
        <input
          id="avgangsplats"
          type="text"
          autoComplete="off"
          placeholder="T.ex. Kärra centrum"
          aria-describedby={errors.departurePlace ? 'avgangsplats-fel' : undefined}
          aria-invalid={errors.departurePlace ? true : undefined}
          {...register('departurePlace')}
        />
        {errors.departurePlace && (
          <p className="form__error" id="avgangsplats-fel">
            {errors.departurePlace.message}
          </p>
        )}
      </div>

      <div className="form__field">
        <label htmlFor="avgangstid">Avgång (svensk tid)</label>
        <input
          id="avgangstid"
          type="datetime-local"
          aria-describedby={errors.departureLocal ? 'avgangstid-fel' : undefined}
          aria-invalid={errors.departureLocal ? true : undefined}
          {...register('departureLocal')}
        />
        {errors.departureLocal && (
          <p className="form__error" id="avgangstid-fel">
            {errors.departureLocal.message}
          </p>
        )}
      </div>

      <div className="form__field">
        <label htmlFor="platser">Lediga platser</label>
        {/* valueAsNumber: en <select> ger sträng, och schemat vill ha ett tal. */}
        <select id="platser" {...register('seats', { valueAsNumber: true })}>
          {Array.from({ length: MAX_SEATS }, (_, index) => index + 1).map((seats) => (
            <option key={seats} value={seats}>
              {seats}
            </option>
          ))}
        </select>
      </div>

      <div className="form__field">
        <label htmlFor="notis">Något mer att säga? (valfritt)</label>
        <textarea
          id="notis"
          rows={2}
          aria-describedby={errors.note ? 'notis-fel' : undefined}
          aria-invalid={errors.note ? true : undefined}
          {...register('note')}
        />
        {errors.note && (
          <p className="form__error" id="notis-fel">
            {errors.note.message}
          </p>
        )}
      </div>

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      <div className="actions">
        <button type="submit" className="button" disabled={isSubmitting}>
          {isSubmitting ? 'Lägger upp…' : 'Lägg upp'}
        </button>
        <button type="button" className="button button--action" onClick={onCancel}>
          Avbryt
        </button>
      </div>
    </form>
  )
}
