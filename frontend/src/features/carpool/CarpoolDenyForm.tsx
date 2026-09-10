import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'

import { MAX_MESSAGE_LENGTH } from './carpoolApi'
import { DENIAL_PHRASES } from './carpoolLabels'

/**
 * Att neka någon en plats.
 *
 * <h3>Ett tyst nej finns inte</h3>
 *
 * Meddelandet är obligatoriskt (§KM.12) och kravet finns även i servern — formuläret
 * håller det bara borta från en onödig vända dit. De färdiga formuleringarna finns för
 * att ett nej som är jobbigt att formulera lätt blir ett nej som aldrig skickas, och den
 * som frågat står och väntar på en plats som aldrig fanns.
 */

const schema = z.object({
  message: z
    .string()
    .trim()
    .min(1, 'Skriv några ord — ett tyst nej ska inte förekomma.')
    .max(MAX_MESSAGE_LENGTH, 'Meddelandet är för långt.'),
})

type FormValues = z.infer<typeof schema>

export function CarpoolDenyForm({
  requestId,
  onSubmit,
  onCancel,
}: {
  requestId: string
  onSubmit: (message: string) => Promise<void>
  onCancel: () => void
}) {
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { message: '' },
  })

  const messageId = `nekande-${requestId}`

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async (values) => {
          try {
            await onSubmit(values.message.trim())
            setFailure(null)
          } catch (error) {
            setFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Svaret är inte skickat — försök igen när du har nät.'
                : 'Svaret gick inte att skicka just nu. Försök igen om en stund.',
            )
          }
        })(event)
      }}
    >
      {/*
        Knappar och inte en lista att välja i: de fyller i textrutan, som sedan går att
        ändra. Valet är alltså en genväg till egna ord, inte ett låst alternativ.
      */}
      <div className="carpool__phrases">
        <p id={`${messageId}-fardiga`} className="carpool__phrases-label">
          Färdiga formuleringar
        </p>
        <div className="actions" role="group" aria-labelledby={`${messageId}-fardiga`}>
          {DENIAL_PHRASES.map((phrase) => (
            <button
              key={phrase}
              type="button"
              className="button button--action"
              onClick={() => {
                setValue('message', phrase, { shouldValidate: true })
              }}
            >
              {phrase}
            </button>
          ))}
        </div>
      </div>

      <div className="form__field">
        <label htmlFor={messageId}>Meddelande</label>
        <textarea
          id={messageId}
          rows={3}
          aria-describedby={errors.message ? `${messageId}-fel` : undefined}
          aria-invalid={errors.message ? true : undefined}
          {...register('message')}
        />
        {errors.message && (
          <p className="form__error" id={`${messageId}-fel`}>
            {errors.message.message}
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
          {isSubmitting ? 'Skickar…' : 'Skicka nekande'}
        </button>
        <button type="button" className="button button--action" onClick={onCancel}>
          Avbryt
        </button>
      </div>
    </form>
  )
}
