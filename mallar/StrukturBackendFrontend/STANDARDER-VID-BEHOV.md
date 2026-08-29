# STANDARDER VID BEHOV — element att införa när kravet finns

> **Till agenten:** Dessa element är **inte** obligatoriska från start. Känn till dem,
> men inför dem **bara när triggern uppfylls** — annars blir det over-engineering (YAGNI).
> När du inför ett element: följ samma mönster och kvalitetsnivå som i [`CLAUDE.md`](./CLAUDE.md).
> Föreslå för användaren när du ser att en trigger är uppfylld, istället för att bygga in det tyst.

Varje element nedan har: **Vad** · **Inför NÄR (trigger)** · **Kort hur**.

---

## 🔵 BACKEND — vid behov

### Pagination, filtrering & sortering
- **Vad:** Sidindela list-endpoints (`PagedResult`), filtrera/sortera på `IQueryable`.
- **Inför NÄR:** så fort en lista kan returnera fler än ~några tiotal rader, eller växer med kundens data.
- **Hur:** `Skip/Take` i databasen, begränsa max `PageSize`, returnera `totalCount`/`totalPages`. *(I praktiken nästan alltid — inför tidigt för list-endpoints.)*

### Caching
- **Vad:** `IMemoryCache` (en instans) eller distribuerad cache / **Redis** (flera instanser).
- **Inför NÄR:** en endpoint är mätbart långsam, läses ofta och ändras sällan; **eller** appen skalas till fler än en instans.
- **Hur:** Cache-aside med TTL + tydlig invalidation vid ändring. Cacha aldrig känslig/användarspecifik data oavsiktligt.

### Background jobs & resiliens
- **Vad:** Bakgrundsjobb (Hangfire / `IHostedService`) + retry/circuit breaker (Polly).
- **Inför NÄR:** uppgifter är långsamma/tunga (mejl, rapporter, import), schemalagda, eller anropar externa tjänster som kan fela.
- **Hur:** Flytta tungt arbete ur request-tråden. Idempotenta jobb. Exponential backoff + circuit breaker + timeouts på externa anrop.

### CI/CD, Docker & deployment
- **Vad:** Containerisering + pipeline (build → test → scan → deploy).
- **Inför NÄR:** så fort projektet ska driftsättas/delas av fler än en utvecklare. *(Inför tidigt — billigt och förebygger deploy-fel.)*
- **Hur:** Multi-stage Dockerfile, CI som faller vid testfel, separata miljöer (Staging→Prod), kontrollerade DB-migrationer, versionstaggning + rollback.

### Domain events / integration events
- **Vad:** Entiteter publicerar domänhändelser; integration events mellan moduler/tjänster.
- **Inför NÄR:** sidoeffekter ska ske vid en domänändring utan att koppla ihop moduler hårt (t.ex. "skicka mejl när order bekräftas").
- **Hur:** Domain events i Domain, hanteras i Application. Outbox-mönster om händelser måste vara tillförlitliga över tjänstegränser.

### Read models / separat läsmodell
- **Vad:** Optimerade läsvyer skilda från skrivmodellen (utbyggd CQRS).
- **Inför NÄR:** läs- och skrivbehoven skiljer sig markant och prestanda kräver det.
- **Hur:** Egna query-modeller/projektioner. Inför **inte** i förväg — det är komplext.

### Rate limiting / throttling
- **Vad:** Begränsa antal requests per klient/IP (ASP.NET inbyggda rate limiter).
- **Inför NÄR:** publika eller oautentiserade endpoints, inloggning, eller API som kan missbrukas/överbelastas.
- **Hur:** Fixed/sliding window eller token bucket per klient. Returnera `429 Too Many Requests` med `Retry-After`. Kombinera med account lockout (se Säkerhet) för inloggning.

### Distribuerad tracing & metrics (OpenTelemetry)
- **Vad:** End-to-end-spårning och mätvärden över tjänstegränser.
- **Inför NÄR:** systemet växer till flera tjänster, eller du behöver felsöka latens/flöden över tjänstegränser.
- **Hur:** OpenTelemetry-instrumentering → exportera till t.ex. Jaeger/Prometheus/Grafana. Bygger vidare på correlation-ID och strukturerad loggning från baslinjen.

### Central pakethantering & feature flags
- **Vad:** `Directory.Packages.props` (central NuGet-versionshantering) respektive feature flags för att slå på/av funktioner i drift.
- **Inför NÄR:** flera projekt/repo:n delar beroenden (central pakethantering); eller funktioner behöver släppas mörkt/gradvis (feature flags).
- **Hur:** Central Package Management samlar paketversioner på ett ställe. Feature flags via config/leverantör — städa bort gamla flaggor när de inte längre behövs.

---

## 🟢 FRONTEND — vid behov

### Global state (Context / Zustand)
- **Vad:** Delad client-state (inloggad användare, tema, kundvagn).
- **Inför NÄR:** samma client-state behövs av komponenter på olika ställen i trädet. *(Inte för sådant som kan vara lokalt.)*
- **Hur:** Context för sällan-ändrad data (memoisera värdet), Zustand för dynamiskt state. Server-state hör hemma i TanStack Query, inte här.

### Performance & optimering
- **Vad:** `React.memo`, `useMemo`, `useCallback`, code-splitting (`lazy` + `Suspense`), list-virtualisering.
- **Inför NÄR:** du har **mätt** ett verkligt problem (Profiler/Lighthouse) — onödiga omrenderingar, stor bundle, långa listor.
- **Hur:** Memoisera dyra komponenter/beräkningar, splittra på route/feature, virtualisera långa listor. Optimera aldrig i förväg.

### Internationalisering (i18n)
- **Vad:** Flerspråksstöd (t.ex. react-i18next).
- **Inför NÄR:** appen ska finnas på fler än ett språk, eller kunden kräver det.
- **Hur:** Inga hårdkodade strängar — allt via översättningsnycklar. Inför tidigt om i18n är känt krav (dyrt att eftermontera).

### Avancerad formulärhantering
- **Vad:** Multi-step wizards, dynamiska fält-arrayer, beroende fält.
- **Inför NÄR:** formulären blir komplexa bortom enkel single-step.
- **Hur:** Bygg vidare på React Hook Form (`useFieldArray`), behåll Zod-validering per steg.

---

## 🔒 SÄKERHET — vid behov (utöver baslinjen i CLAUDE.md)

> Baslinjen (AuthN/AuthZ, secrets, input-validering, CORS/HTTPS, XSS-default, audit, GDPR-grund,
> dependency-scan) ligger i `CLAUDE.md` och gäller alltid. Nedan är fördjupningar.

### Fältkryptering i vila (PII)
- **Vad:** Kryptera specifika känsliga kolumner i databasen (personnummer, hälsodata).
- **Inför NÄR:** du lagrar särskilt känsliga personuppgifter eller kunden/regelverk kräver det.
- **Hur:** Kryptera på applikationsnivå eller via DB-funktion; nycklar i Key Vault, med rotation.

### CSRF-skydd
- **Vad:** Anti-forgery tokens.
- **Inför NÄR:** du använder **cookie-baserad** auth (med ren bearer-token i header behövs det normalt inte).
- **Hur:** Anti-forgery token + `SameSite`-cookies.

### Account lockout / brute-force-skydd
- **Vad:** Lås konto / fördröj efter upprepade misslyckade inloggningar.
- **Inför NÄR:** du har egen inloggning (inte enbart extern IdP).
- **Hur:** Räkna misslyckanden, exponentiell fördröjning/lockout, kombinera med rate limiting.

### SAST/DAST & säkerhetstestning i pipeline
- **Vad:** Statisk (kod) och dynamisk (körande app) säkerhetsanalys.
- **Inför NÄR:** inför produktionssläpp / kundkrav på säkerhetsrevision.
- **Hur:** Lägg till i CI (t.ex. CodeQL, OWASP ZAP). Åtgärda fynd före release.

### Secrets-rotation
- **Vad:** Rotera nycklar/lösenord regelbundet utan nedtid.
- **Inför NÄR:** produktion med långlivade hemligheter och säkerhetskrav.
- **Hur:** Key Vault med versionerade secrets, stöd för flera giltiga nycklar under övergång.

### Penetrationstest & säkerhetsgranskning
- **Vad:** Extern säkerhetsgranskning.
- **Inför NÄR:** inför leverans till kund eller vid känd känslig data.
- **Hur:** Beställ extern pen-test, åtgärda fynd, dokumentera för kundens revision.

---

## 🧭 Snabb beslutsregel för agenten

> **"Behöver detta projekt elementet *nu*, eller löser jag ett problem jag inte har än?"**
> Är svaret det senare → nämn elementet för användaren och vänta. Bygg inte in det i förväg.
