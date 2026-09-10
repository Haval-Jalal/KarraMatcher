import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'

import { updateName, type AccountProfile } from './authApi'

/**
 * Namnet på kontot (`#154`).
 *
 * <h3>Varför appen frågar</h3>
 *
 * Servern lagrade länge bara en mejladress, och samåkningen blev därmed anonym: "någon
 * frågar om skjuts". Mellan grannar som möts på planen nästa lördag är det ett tomrum.
 * Namnet fyller det, och används till ingenting annat.
 *
 * <h3>Efternamnet är valfritt</h3>
 *
 * I ett föräldralag räcker förnamnet nästan alltid, och det som inte behövs ska inte
 * krävas. Den som vill skilja två Anna åt fyller i det.
 *
 * <h3>Vem som ser det</h3>
 *
 * Texten säger det rakt ut, för det är den frågan en förälder faktiskt har innan hen
 * skriver in sitt namn: bara inloggade i laget, aldrig den som bara tittar på matchtiden.
 */

const schema = z.object({
  firstName: z.string().trim().min(1, 'Skriv ditt förnamn.').max(60, 'Förnamnet är för långt.'),
  lastName: z.string().trim().max(60, 'Efternamnet är för långt.'),
})

type FormValues = z.infer<typeof schema>

export function NameForm({
  profile,
  submitLabel,
  onSaved,
  onSkip,
}: {
  profile: AccountProfile | null
  submitLabel: string
  onSaved: (profile: AccountProfile) => void
  /** Visas bara när det finns någonstans att hoppa till. Namnet går att fylla i senare. */
  onSkip?: (() => void) | undefined
}) {
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      firstName: profile?.firstName ?? '',
      lastName: profile?.lastName ?? '',
    },
  })

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async (values) => {
          try {
            const saved = await updateName(
              values.firstName.trim(),
              values.lastName.trim() === '' ? null : values.lastName.trim(),
            )

            setFailure(null)
            onSaved(saved)
          } catch (error) {
            setFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Namnet är inte sparat — försök igen när du har nät.'
                : 'Namnet gick inte att spara just nu. Försök igen om en stund.',
            )
          }
        })(event)
      }}
    >
      <div className="form__field">
        <label htmlFor="fornamn">Förnamn</label>
        <input
          id="fornamn"
          type="text"
          autoComplete="given-name"
          aria-describedby={errors.firstName ? 'fornamn-fel' : undefined}
          aria-invalid={errors.firstName ? true : undefined}
          {...register('firstName')}
        />
        {errors.firstName && (
          <p className="form__error" id="fornamn-fel">
            {errors.firstName.message}
          </p>
        )}
      </div>

      <div className="form__field">
        <label htmlFor="efternamn">Efternamn (valfritt)</label>
        <input
          id="efternamn"
          type="text"
          autoComplete="family-name"
          aria-describedby={errors.lastName ? 'efternamn-fel' : undefined}
          aria-invalid={errors.lastName ? true : undefined}
          {...register('lastName')}
        />
        {errors.lastName && (
          <p className="form__error" id="efternamn-fel">
            {errors.lastName.message}
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
          {isSubmitting ? 'Sparar…' : submitLabel}
        </button>

        {onSkip !== undefined && (
          <button type="button" className="button button--action" onClick={onSkip}>
            Senare
          </button>
        )}
      </div>
    </form>
  )
}
