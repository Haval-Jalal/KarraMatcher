import type { SVGProps } from 'react'

/**
 * Botten-navigeringens ikoner (`#redesign`).
 *
 * Egna linje-ikoner i stället för emoji — emoji renderas olika per plattform, hänger inte ihop
 * som ett set, och drar ner hela appen till "tidigt 2000-tal". Alla ritar med `currentColor`, så
 * temat och aktiv-färgen styr dem, och de är dekorativa (`aria-hidden`) eftersom etiketten bär
 * texten.
 */
const shared: SVGProps<SVGSVGElement> = {
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.9,
  strokeLinecap: 'round',
  strokeLinejoin: 'round',
  'aria-hidden': true,
  focusable: false,
}

export function HomeIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg {...shared} {...props}>
      <path d="M4 11.2 12 4.5l8 6.7" />
      <path d="M6 10v9a1 1 0 0 0 1 1h3v-5h4v5h3a1 1 0 0 0 1-1v-9" />
    </svg>
  )
}

export function ActivityIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg {...shared} {...props}>
      <rect x="3.5" y="5" width="17" height="15" rx="3" />
      <path d="M3.5 9.5h17M8 3.5v3M16 3.5v3" />
      <circle cx="12" cy="14.5" r="1.4" fill="currentColor" stroke="none" />
    </svg>
  )
}

export function ChatIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg {...shared} {...props}>
      <path d="M4 6.5A2.5 2.5 0 0 1 6.5 4h11A2.5 2.5 0 0 1 20 6.5v7A2.5 2.5 0 0 1 17.5 16H9l-4 3.5V6.5Z" />
    </svg>
  )
}

export function TrophyIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg {...shared} {...props}>
      <path d="M7 4h10v3.5a5 5 0 0 1-10 0Z" />
      <path d="M7 5H4v1.5A3.5 3.5 0 0 0 7 10M17 5h3v1.5A3.5 3.5 0 0 1 17 10M9.5 19.5h5M12 12.5v4" />
    </svg>
  )
}

export function StarIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg {...shared} {...props} strokeWidth={1.8}>
      <path d="m12 3.6 2.5 5 5.6.8-4 4 1 5.6-5.1-2.7-5.1 2.7 1-5.6-4-4 5.6-.8Z" />
    </svg>
  )
}

export function MenuIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg {...shared} {...props}>
      <path d="M4 7h16M4 12h16M4 17h16" />
    </svg>
  )
}
