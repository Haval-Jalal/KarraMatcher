import { type VercelConfig } from '@vercel/config/v1'

/*
 * Vercel-config som kod (`#648`-serien → staging, §KM.11).
 *
 * Hela poängen med att detta är en .ts och inte en .json: rewrite-malet lases ur
 * miljovariabeln KARRA_API_URL, sa att SAMMA fil kan peka pa prod eller staging utan att
 * divergera mellan grenarna. Da mergar staging -> main rent, utan konflikt pa en rewrite-rad.
 *
 *   prod-Vercel:    KARRA_API_URL = https://karramatcher-api.onrender.com
 *   staging-Vercel: KARRA_API_URL = https://karramatcher-api-staging.onrender.com
 *
 * Fallback = prod-URL:en. Darfor beter sig prod IDENTISKT aven om varen inte ar satt (den
 * hardkodades forr har), sa utrullningen av den har andringen kan aldrig valta prod-deployen.
 * Staging MASTE dock satta sin egen var -- gor den inte det pratar staging med prod (se
 * docs/STAGING-MILJO.md). Render-URL:en bor nu alltsa i Vercel-varen, inte i filen (§KM.11).
 */
const API_URL = process.env.KARRA_API_URL ?? 'https://karramatcher-api.onrender.com'

export const config: VercelConfig = {
  framework: 'vite',
  buildCommand: 'npm run build',
  outputDirectory: 'dist',
  rewrites: [
    // Klienten ser en enda origin: /api proxas till backend (§KM.11). Forstaparts-cookien
    // for refresh-token halls darfor forstaparts -- hela poangen med proxyn.
    { source: '/api/:path*', destination: `${API_URL}/api/:path*` },
    // SPA-fallback: alla ovriga vagar lamnas till klient-routern.
    { source: '/(.*)', destination: '/index.html' },
  ],
  // Kvallspaminnelsen (Vercel Hobby tillater cron en gang per dygn, §KM.11).
  crons: [{ path: '/api/v1/jobs/match-reminders', schedule: '0 18 * * *' }],
  headers: [
    {
      source: '/(.*)',
      headers: [
        {
          key: 'Content-Security-Policy',
          value:
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' https://api.open-meteo.com; manifest-src 'self'; worker-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'",
        },
        { key: 'X-Content-Type-Options', value: 'nosniff' },
        { key: 'X-Frame-Options', value: 'DENY' },
        { key: 'Referrer-Policy', value: 'no-referrer' },
        { key: 'Strict-Transport-Security', value: 'max-age=63072000; includeSubDomains' },
      ],
    },
  ],
}
