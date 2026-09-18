import { detectMapsPlatform, directionsDestination, directionsUrl } from '@/lib/maps'

interface DirectionsLinkProps {
  venueName: string
  address?: string | null
}

/**
 * Knapp som öppnar vägbeskrivning i enhetens kartapp.
 *
 * `rel="noopener noreferrer"` är inte valfritt (Säkerhetschecklistan 5.6). Utan `noopener`
 * får den öppnade sidan en referens tillbaka till vårt fönster och kan styra om det;
 * `noreferrer` hindrar dessutom att vår adress läcker till kartleverantören.
 */
export function DirectionsLink({ venueName, address }: DirectionsLinkProps) {
  const destination = directionsDestination(venueName, address)
  const href = directionsUrl(destination, detectMapsPlatform())

  return (
    <a className="button button--action" href={href} target="_blank" rel="noopener noreferrer">
      Vägbeskrivning
      <span className="visually-hidden"> till {destination}, öppnas i kartappen</span>
    </a>
  )
}
