# SÄKERHETS-CHECKLISTA — auditerbar (telefonapp + backend)

> **Syfte:** Konkret, avbockbar lista som visar att appen och backend uppfyller en hög säkerhetsnivå.
> Mappad mot **OWASP MASVS** (mobil), **OWASP ASVS** + **API Security Top 10** (backend) och **App Store / Google Play**-krav.
> Använd som grind före varje release och som underlag vid kund-/revisionsgranskning.
>
> **Status per rad:** `✅ klar` · `🟡 pågår` · `⬜ ej börjad` · `➖ ej tillämpligt (motivera)`.
> Det mesta nedan är **baslinje** (från [`CLAUDE.md`](./CLAUDE.md)); rader märkta *(vid behov)* införs när triggern uppfylls (se [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md)).

---

## 1. Autentisering & sessioner
| # | Kontroll | Status |
|---|----------|:------:|
| 1.1 | Lösenord hashas (BCrypt/Identity), aldrig klartext | ⬜ |
| 1.2 | Lösenordspolicy (längd/styrka) + kontroll mot läckta lösenord | ⬜ |
| 1.3 | E-postverifiering vid registrering | ⬜ |
| 1.4 | Säker lösenordsåterställning (tidsbegränsad engångstoken) | ⬜ |
| 1.5 | JWT validerar issuer/audience/lifetime/signing key | ⬜ |
| 1.6 | Refresh tokens med rotation + återanvändnings-detektering | ⬜ |
| 1.7 | Token-revocation vid utloggning / lösenordsbyte | ⬜ |
| 1.8 | Auto-utloggning vid inaktivitet (känsliga appar) | ⬜ |
| 1.9 | Account lockout / brute-force-skydd *(vid behov)* | ⬜ |
| 1.10 | MFA / 2FA *(vid behov)* | ⬜ |

## 2. Auktorisering (åtkomstkontroll)
| # | Kontroll | Status |
|---|----------|:------:|
| 2.1 | Policy-baserad auktorisering (inga hårdkodade rollkontroller) | ⬜ |
| 2.2 | **Objektnivå-auktorisering på varje resurs (mot IDOR)** | ⬜ |
| 2.3 | Funktionsnivå-auktorisering (admin-endpoints skyddade) | ⬜ |
| 2.4 | Principen om minsta behörighet genomgående | ⬜ |
| 2.5 | Ingen "mass assignment" — DTOs, ej entiteter, i API-in/ut | ⬜ |

## 3. Datalagring på enheten (MASVS-STORAGE)
| # | Kontroll | Status |
|---|----------|:------:|
| 3.1 | Tokens/secrets endast i `expo-secure-store` (Keychain/Keystore) | ⬜ |
| 3.2 | Inga secrets i `AsyncStorage`, klartextfil eller global state | ⬜ |
| 3.3 | Känsliga data exkluderade från iCloud/Android-molnbackup | ⬜ |
| 3.4 | Minimal lokal PII; lokal data rensas vid utloggning | ⬜ |
| 3.5 | Inga hemligheter hårdkodade i appbinären (verifierat) | ⬜ |
| 3.6 | Inga secrets/PII/tokens i loggar eller crashrapporter (scrubbat) | ⬜ |

## 4. Nätverk & transport (MASVS-NETWORK)
| # | Kontroll | Status |
|---|----------|:------:|
| 4.1 | All trafik över TLS/HTTPS (inga klartext-anrop) | ⬜ |
| 4.2 | HSTS påtvingat på backend | ⬜ |
| 4.3 | CORS låst till kända origins | ⬜ |
| 4.4 | Certificate pinning *(vid behov — känsliga appar)* | ⬜ |
| 4.5 | Webhook-signaturer verifieras (inkommande) | ⬜ |
| 4.6 | SSRF-skydd på utgående anrop som styrs av indata | ⬜ |

## 5. Plattform & klientintegritet (MASVS-PLATFORM/RESILIENCE)
| # | Kontroll | Status |
|---|----------|:------:|
| 5.1 | Behörigheter begärs just-in-time med förklaring; nekad hanteras | ⬜ |
| 5.2 | Känsliga skärmar dolda i app-växlare / skärmdump blockerad | ⬜ |
| 5.3 | Känsliga fält: `secureTextEntry`, ingen tangentbordscache/autofyll | ⬜ |
| 5.4 | Lösenord/tokens läggs aldrig i urklipp | ⬜ |
| 5.5 | Deep links & push-payloads valideras/auktoriseras server-side | ⬜ |
| 5.6 | WebView härdad om använd *(vid behov)* | ⬜ |
| 5.7 | Root-/jailbreak-detektering + anti-tampering *(vid behov)* | ⬜ |
| 5.8 | Biometrisk inloggning där det passar | ⬜ |

## 6. Indata, kod & vanliga sårbarheter
| # | Kontroll | Status |
|---|----------|:------:|
| 6.1 | All input valideras server-side (klient = endast UX) | ⬜ |
| 6.2 | Parametriserade queries / EF → ingen SQL-injection | ⬜ |
| 6.3 | Output-escaping (RN/React default) — ingen osäker HTML-injektion | ⬜ |
| 6.4 | Säkerhetsheaders satta (CSP där relevant, nosniff, frame-options) | ⬜ |
| 6.5 | Rate limiting på publika/inloggnings-endpoints *(vid behov)* | ⬜ |
| 6.6 | Inga interna fel/stack traces läcker till klient (ProblemDetails) | ⬜ |

## 7. Secrets & konfiguration
| # | Kontroll | Status |
|---|----------|:------:|
| 7.1 | Secrets i secret store / Key Vault — aldrig i repo | ⬜ |
| 7.2 | `.env`/signeringsnycklar aldrig incheckade (blockerat av hook) | ⬜ |
| 7.3 | Separat config per miljö (dev/staging/prod) | ⬜ |
| 7.4 | Signeringshemligheter (iOS/Android) hanteras av EAS, ej i repo | ⬜ |
| 7.5 | Secrets-rotation *(vid behov)* | ⬜ |

## 8. Loggning, audit & övervakning
| # | Kontroll | Status |
|---|----------|:------:|
| 8.1 | Strukturerad loggning med correlation-ID | ⬜ |
| 8.2 | Audit-logg för känsliga åtgärder (vem/vad/när), oföränderlig | ⬜ |
| 8.3 | Larm på säkerhetshändelser (inloggningsfel, behörighetsavslag) | ⬜ |
| 8.4 | Crashrapportering aktiv, PII-scrubbad | ⬜ |

## 9. Integritet, GDPR & app store
| # | Kontroll | Status |
|---|----------|:------:|
| 9.1 | Personuppgifter identifierade + laglig grund | ⬜ |
| 9.2 | Dataminimering (samlar bara nödvändigt) | ⬜ |
| 9.3 | Konto- & databorttagning i appen ("rätt att bli glömd") | ⬜ |
| 9.4 | Synlig integritetspolicy | ⬜ |
| 9.5 | iOS Privacy Nutrition Labels korrekt ifyllda | ⬜ |
| 9.6 | Android Data Safety-formulär korrekt ifyllt | ⬜ |
| 9.7 | App Tracking Transparency om spårning sker (iOS) | ⬜ |
| 9.8 | Fältkryptering i vila för särskilt känslig PII *(vid behov)* | ⬜ |

## 10. Beroenden & leveranskedja
| # | Kontroll | Status |
|---|----------|:------:|
| 10.1 | `dotnet list package --vulnerable` rent i CI | ⬜ |
| 10.2 | `npm audit` utan kända allvarliga sårbarheter | ⬜ |
| 10.3 | Automatiska uppdaterings-PR:ar (Dependabot/Renovate) | ⬜ |
| 10.4 | Expo SDK på en aktuell, stödd version | ⬜ |

## 11. Drift & återhämtning
| # | Kontroll | Status |
|---|----------|:------:|
| 11.1 | Health checks (`/health`, `/health/ready`) | ⬜ |
| 11.2 | DB-backup automatisk + **återställning testad** | ⬜ |
| 11.3 | Minsta-version-endpoint + force-update i appen | ⬜ |
| 11.4 | Maintenance-läge hanteras i appen | ⬜ |

## 12. Testning & verifiering (före release)
| # | Kontroll | Status |
|---|----------|:------:|
| 12.1 | Säkerhetsrelaterade enhetstester (authz, validering) gröna | ⬜ |
| 12.2 | Arkitekturtester skyddar lagergränser (NetArchTest) | ⬜ |
| 12.3 | SAST/DAST + mobil-scanner (MobSF) *(vid behov, före prod)* | ⬜ |
| 12.4 | Penetrationstest *(vid behov, före kundleverans)* | ⬜ |
| 12.5 | Testad på fysiska iOS- och Android-enheter | ⬜ |

---

> **Releasegrind:** Alla baslinjerader ska vara `✅` (eller `➖` med motivering) innan en produktionsrelease.
> *(vid behov)*-rader bockas av när deras trigger uppfyllts. Uppdatera statusen i samma PR som åtgärden.
