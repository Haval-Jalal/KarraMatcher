import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate, useSearch } from '@tanstack/react-router'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { requestLoginCode, verifyLoginCode } from './authApi'
import { useAuth } from './useAuth'

/**
 * Inloggning i två steg: adress, sedan kod.
 *
 * <h3>Vad den ska kännas som</h3>
 *
 * En förälder gör det här en gång per telefon och ska sedan aldrig tänka på det igen.
 * Därför inga lösenord, inga villkor att bocka i, och ingenting att komma ihåg — bara en
 * adress och sex siffror ur ett mejl.
 *
 * <h3>Varför texten inte säger om adressen fanns</h3>
 *
 * "Vi har skickat en kod om adressen finns hos oss" är avsiktligt luddigt. Servern svarar
 * likadant oavsett, och gränssnittet får inte avslöja det servern håller tyst om —
 * annars blir inloggningsrutan en adresslista för den som frågar tillräckligt många
 * gånger.
 */

/**
 * Vart en lyckad inloggning ska leda.
 *
 * <h3>Varför kontrollen ligger här och inte bara i routern</h3>
 *
 * Routens `validateSearch` rensar den typade sökträngen, men den är inte det som avgör
 * vart `navigate` går. Kontrollen hör hemma där omdirigeringen faktiskt sker — annars
 * skyddar den bara så länge ingen läser värdet någon annanstans.
 *
 * En open redirect är som dyrast precis efter en inloggning: den som klickat på länken
 * litar på sidan hen kommer till. Allt som inte är en adress inuti appen kastas därför
 * bort. `//` räknas som utanför — det är en protokollrelativ adress till en annan värd.
 */
function safeDestination(next: unknown): string {
  return typeof next === 'string' && next.startsWith('/') && !next.startsWith('//') ? next : '/'
}

const emailSchema = z.object({
  email: z.string().min(1, 'Fyll i din mejladress.').email('Mejladressen ser inte riktig ut.'),
})

const codeSchema = z.object({
  code: z
    .string()
    .min(1, 'Fyll i koden från mejlet.')
    .regex(/^\d{6}$/, 'Koden är sex siffror.'),
})

type EmailForm = z.infer<typeof emailSchema>
type CodeForm = z.infer<typeof codeSchema>

export function LoginPage() {
  const [email, setEmail] = useState<string | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  const navigate = useNavigate()
  const search = useSearch({ from: '/logga-in' })
  const { refresh } = useAuth()

  useDocumentTitle('Logga in')

  return (
    <main>
      <header className="app-header">
        <h1>Logga in</h1>
        <p className="app-header__subtitle">
          Du behöver bara ett konto för att lägga upp samåkning eller sköta ett lag. Matchtiderna är
          öppna för alla.
        </p>
      </header>

      {email === null ? (
        <EmailStep
          onSent={(sent) => {
            setEmail(sent)
            setFailure(null)
          }}
          onFailure={setFailure}
        />
      ) : (
        <CodeStep
          email={email}
          onVerified={() => {
            refresh()
            void navigate({ to: safeDestination(search.next) })
          }}
          onFailure={setFailure}
          onStartOver={() => {
            setEmail(null)
            setFailure(null)
          }}
        />
      )}

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}
    </main>
  )
}

function EmailStep({
  onSent,
  onFailure,
}: {
  onSent: (email: string) => void
  onFailure: (message: string) => void
}) {
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<EmailForm>({ resolver: zodResolver(emailSchema) })

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async ({ email }) => {
          try {
            await requestLoginCode(email)
            onSent(email)
          } catch (error) {
            onFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
                : 'Kunde inte skicka koden just nu. Försök igen om en stund.',
            )
          }
        })(event)
      }}
    >
      <div className="form__field">
        <label htmlFor="epost">Mejladress</label>
        <input
          id="epost"
          type="email"
          inputMode="email"
          autoComplete="email"
          aria-describedby={errors.email ? 'epost-fel' : undefined}
          aria-invalid={errors.email ? true : undefined}
          {...register('email')}
        />
        {errors.email && (
          <p className="form__error" id="epost-fel">
            {errors.email.message}
          </p>
        )}
      </div>

      <button type="submit" className="button" disabled={isSubmitting}>
        {isSubmitting ? 'Skickar…' : 'Skicka kod'}
      </button>
    </form>
  )
}

function CodeStep({
  email,
  onVerified,
  onFailure,
  onStartOver,
}: {
  email: string
  onVerified: () => void
  onFailure: (message: string) => void
  onStartOver: () => void
}) {
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<CodeForm>({ resolver: zodResolver(codeSchema) })

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async ({ code }) => {
          try {
            await verifyLoginCode(email, code)
            onVerified()
          } catch (error) {
            onFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
                : 'Koden stämmer inte, eller har gått ut. Begär en ny om det dröjt en stund.',
            )
          }
        })(event)
      }}
    >
      <p className="state" role="status">
        Om <strong>{email}</strong> finns hos oss har vi skickat en kod dit. Den gäller i tio
        minuter.
      </p>

      <div className="form__field">
        <label htmlFor="kod">Kod från mejlet</label>
        <input
          id="kod"
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={6}
          aria-describedby={errors.code ? 'kod-fel' : undefined}
          aria-invalid={errors.code ? true : undefined}
          {...register('code')}
        />
        {errors.code && (
          <p className="form__error" id="kod-fel">
            {errors.code.message}
          </p>
        )}
      </div>

      <div className="actions">
        <button type="submit" className="button" disabled={isSubmitting}>
          {isSubmitting ? 'Kontrollerar…' : 'Logga in'}
        </button>
        <button type="button" className="button" onClick={onStartOver}>
          Byt adress
        </button>
      </div>
    </form>
  )
}
