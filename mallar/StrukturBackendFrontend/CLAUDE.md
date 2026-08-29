# CLAUDE.md — Projektstandard (MÅSTE från start)

> **📌 Läs först:** [`docs/PROJEKT-HANDOFF.md`](./docs/PROJEKT-HANDOFF.md) — aktuell projektstatus,
> beslut, öppna frågor och nästa steg. Skriv ingen kod förrän ett issue är valt och godkänt.

> **Till agenten:** Detta är projektets regelverk. Följ det i **all** kod du genererar.
> Dessa element är obligatoriska från dag 1 — de utgör själva strukturen.
> Element som läggs till *vid behov* finns i [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md) —
> läs den filen också så du vet när de ska införas, men implementera dem inte i förväg (YAGNI).
> Djupgående förklaringar och kodexempel finns i `index.html` (referensdokument för människor).

**Teknikstack:** Backend = C# / .NET (senaste LTS), EF Core, MediatR, FluentValidation.
Frontend = React + TypeScript, TanStack Router, TanStack Query, React Hook Form + Zod.

---

## Infrastruktur & Claude Code-konfiguration

Projektets `CLAUDE.md` är **projektlagret** i ett tvånivåsystem. Känn till samverkan:

| Nivå | Plats | Vad den styr |
|------|-------|--------------|
| **Globalt** | `~/.claude/` | Kärnprinciper, agentteam, minne, hooks, inställningar — gäller *alla* projekt |
| **Projekt** | detta `CLAUDE.md` + `STANDARDER-VID-BEHOV.md` | Projektspecifika krav — har **företräde** vid konflikt |

Vad det globala lagret bidrar med till det här projektet:
- **Hooks (`~/.claude/settings.json`)** — `PreToolUse` blockerar tekniskt direktpush till `main` och läsning av `*.secrets*`/`*.env*`-filer (förstärker §0 p. 7). `SessionStart` injicerar globala standarder automatiskt vid sessionsstart.
- **Agenter (`~/.claude/agents/`)** — Specialister som `reviewer`, `security_reviewer` och `debugger` delegeras vid rätt tillfälle (se §0).
- **Minne (`~/.claude/memory/`)** — Rättelser och beslut sparas automatiskt och gäller nästa session (se §0).
- **`standards/` (globalt)** — detaljerade regler som är för långa för `CLAUDE.md` injiceras härifrån via `SessionStart`-hooken. Projektlagrets motsvarighet är [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md).
- **Inställningar** — `effortLevel` är en direkt avvägning mellan kvalitet, hastighet och kostnad: `"high"` för arkitektur och felsökning, `"medium"` för rutinändringar. Kan justeras i farten via `/config` eller `/fast`. `deny`-listan ska blockera `*.secrets*`, `*.env*` och `git push --force`.
- **MCP-servrar** — GitHub-integration (om konfigurerad) ger direktåtkomst till issues, PRs och projektboard utan manuella `gh`-kommandon.
- **Skills** — Inbyggda förmågor som `/code-review` och `/security-review` laddas vid behov utan att ta permanent kontext.
- **`settings.local.json`** — maskinspecifika inställningar (t.ex. sökvägar eller nycklar som skiljer sig mellan datorer) läggs här och checkas **aldrig** in i git. Skilj från `settings.json` som gäller hela teamet.
- **`/fast`-läge** — snabbare svar utan att byta ned till en mindre modell. Bra för rutinändringar; kombinera med lägre `effortLevel` för repetitivt arbete.
- **Slash-kommandon (inbyggda)** — `/model` (byt modell), `/config` (inställningar), `/clear` (rensa kontext). Dessa är skilda från skills-baserade kommandon som `/code-review`.
- **Context-komprimering** — vid långa sessioner sammanfattas äldre delar automatiskt så att arbetet kan fortsätta utan att kontext tar slut. Kör gärna långa sessioner utan oro.
- **`/schedule` och `/loop`** — `/schedule` kör en uppgift på ett schema (cron-likt); `/loop` upprepar något med ett intervall. Användbart för återkommande kontroller eller bakgrundsarbete.

> Generella regler och verktyg konfigureras globalt. Projektspecifika krav — teknikstack, DoD, branschregler, BE/FE-koordinering — hör hemma i detta dokument.

> **Hook-principen:** allt du annars måste säga *"kom ihåg att…"* varje session → gör det till en hook i `~/.claude/settings.json`. Hooks körs av verktyget, inte AI:n — de går inte att "prata bort".

---

## 0. Så här arbetar du (process — gäller alltid)

1. **Plan först.** Presentera en kort plan och invänta godkännande innan du skriver mycket kod.
2. **Bygg i vertical slices** — en feature hela vägen (domän → handler → validator → API → frontend → tester) innan nästa. Inte allt på en gång.
3. **Tester per steg.** Skriv tester med koden och kör dem innan du går vidare.
4. **Litet och fokuserat.** Små klasser/komponenter, ett ansvar (SRP). Bryt ut tidigt.
5. **YAGNI.** Lägg inte till element från `STANDARDER-VID-BEHOV.md` förrän behovet faktiskt finns.
6. **Konsekvens > smarthet.** Följ befintliga mönster i kodbasen.
7. **Branch per ändring — aldrig direktpush till `main`.** Saknas branch protection (t.ex. privat repo utan GitHub Pro), så skapa **alltid** en egen branch *innan* du ändrar något (t.ex. `feature/<kort-namn>`, `fix/...`, `docs/...`). Pusha **aldrig** direkt till `main`. Sammanslagning till `main` sker via PR (se punkt 8).
8. **Review och merge av PR är ALLTID manuellt (människa).** Claude/agenten får **absolut inte** granska (review), godkänna (approve) eller merga en PR — och får **inte heller fråga** om det är okej att göra det. Agentens tillåtna steg är: skapa branch → gör ändringar → committa → **pusha** branchen (och får öppna en PR). Men **review, approve och merge görs alltid av en människa** — aldrig av agenten.
9. **Projektboard uppdateras alltid — utan att bli påmind.**
   - Innan första kodraden: flytta issue till **`In Progress`** på boarden (`gh project item-edit … --single-select-option-id <In Progress>`).
   - När PR öppnas: flytta issue till **`In Review`**.
   - När PR är mergad och issue stängs: flytta issue till **`Done`** och stäng issue på GitHub (`gh issue close`).
10. **Branch-hygien mellan issues.** Innan en ny issue påbörjas: `git checkout main && git pull` (i både BE och FE), radera den lokala branchen (`git branch -d <branch>`). Börja aldrig en ny feature från en gammal branch.
11. **Kör lint och bygge lokalt innan commit** — CI ska inte vara det som fångar formateringsfel.
    - FE: `npm run lint` (Prettier + ESLint, `--max-warnings 0`).
    - BE: `dotnet build` (varningsfritt) + `dotnet test` + `dotnet format`.
12. **Välj nästa issue från boarden.** Läs projektets plan-/roadmap-dokument (t.ex. [`docs/MVP-PLAN.md`](./docs/MVP-PLAN.md)) för att avgöra om ett issue hör till MVP-kärnan eller post-MVP. Ta alltid issues ur `Ready`-kolumnen på projektets board (`[länk till GitHub Projects-board]`) — aldrig direkt från Backlog utan att de godkänts av människa.
13. **Verifiera BE-kontrakt innan FE-feature startas.** Om en FE-feature kräver ett API-anrop — kontrollera att motsvarande BE-endpoint faktiskt finns (rätt metod, URL, request/response-form) innan FE-branchen skapas. Saknas något: bygg BE-gapet först i en separat branch och PR, merga den, och starta sedan FE.
14. **Uppdatera PROJEKT-HANDOFF.md när issues eller milstolpar stängs.** Handoff-dokumentet är det första som läses i varje ny session. Lägg till avklarade issues under "Klart hittills" och uppdatera "Nästa steg" så dokumentet alltid speglar faktisk status — gör det i samma commit som övriga ändringar för ett issue, eller i en separat `docs/handoff-...`-branch om det är en större uppdatering.
15. **Dessa procesregler gäller i båda repon.** CLAUDE.md finns i både BE och FE. Ändringar i procesreglerna (§0) ska speglas i båda. Om du bara jobbar i ett repo men ser att ett processregel saknas i det andra — notera det men ändra inte utan att fråga.
16. **Aktivera Plan mode för icke-triviala uppgifter.** Rör uppgiften arkitektur, ett nytt mönster, flera lager eller infrastruktur — aktivera Plan mode (`/plan`) och visa planen för godkännande *innan* kod skrivs. Stärker regel 1 med ett tekniskt steg.
17. **Delegera till specialistagenter vid rätt tillfälle.** Kodgranskning → `reviewer`-agenten. Säkerhetsanalys → `security_reviewer`. Rotorsaksanalys → `debugger`. Delegering håller huvud-agenten fokuserad och sparar kontext.
18. **Spara rättelser i memory direkt.** När du rättar agentens beteende och regeln ska gälla framåt — be agenten spara det som feedback-minne i `~/.claude/memory/` direkt, inte "senare".
19. **Commit-konvention (Conventional Commits).** Commit-meddelanden följer `typ(scope): beskrivning` — `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`. Imperativ ton, rubrikrad ≤ 72 tecken, kropp för *varför* vid behov. Referera issue (`#12`) när det finns. Ger läsbar historik och möjliggör automatisk changelog.

---

## 🔵 BACKEND (C#) — måste från start

### Arkitektur & struktur
- **Clean Architecture.** Lager: `Domain → Application → Infrastructure → Api`. Beroenden pekar **alltid inåt**.
- `Domain` har **noll** ramverksberoenden (ingen EF, ingen ASP.NET). Affärsregler bor i domänen, inte i controllers.
- Interfaces definieras i `Application`, implementeras i `Infrastructure`.
- **Organisera per feature/modul** (`Features/Orders/...`), inte per teknisk typ.
- **Dependency Injection** via konstruktor — inga `new` på beroenden. Varje lager har en `AddXxx`-extension. Rätt livstid (`Scoped` för DbContext).

### Use cases & data
- **CQRS + MediatR.** Commands ändrar state, Queries läser. En handler per use case. Controllers är tunna och delegerar bara (`_mediator.Send`).
- **DTOs (records)** för all in/utdata — exponera **aldrig** entiteter i API:t. Mapping på ett ställe.
- **Repository-interfaces i Application**, EF-implementation i Infrastructure. `AsNoTracking()` för read-only. Async + `CancellationToken` genomgående.

### Databas & migrations
- **EF Core Migrations från start.** Alla schemaändringar sker via migrations — **aldrig** manuella ändringar direkt i databasen.
- **Migrations checkas in** i repo och körs kontrollerat (`dotnet ef database update`, eller automatiskt vid deploy). **Redigera aldrig** en migration som redan körts i en delad/produktionsmiljö — skapa en ny.
- En migration per logisk schemaändring med beskrivande namn. Granska genererad SQL innan den körs mot en delad miljö. Seed-data hanteras kontrollerat och idempotent.

### Validering & fel
- **FluentValidation** — en validator per command/query med input, körs automatiskt via en `ValidationBehavior`.
- **Global exception-middleware** som mappar fel till **ProblemDetails (RFC 7807)** med rätt statuskod. Inga stack traces till klient. Result-mönster för förväntade fel.

### Loggning, drift & observability
- **Strukturerad loggning** (Serilog e.d.) med konsekventa nivåer (`Information`/`Warning`/`Error`) — aldrig `Console.WriteLine`. Logga som strukturerade fält, inte hopslagna strängar.
- **Correlation-/request-ID** sätts per request och propageras genom loggarna (och vidare till externa anrop), så ett ärende kan följas end-to-end.
- **Health checks** — `/health` (liveness) och `/health/ready` (readiness, inkl. DB-koll) från start. Används av container-orkestrering och uptime-övervakning.
- **Logga aldrig** secrets, tokens eller PII i klartext (se Säkerhetsbaslinje). Driftloggning är skild från audit-loggning (vem/vad/när), som hanteras enligt baslinjen.

### Säkerhet (se även "Säkerhetsbaslinje" nedan)
- **Authentication & Authorization** från start: JWT med validering av issuer/audience/lifetime/signing key. **Policy-baserad** auktorisering, inte hårdkodade rollkontroller. `UseAuthentication()` före `UseAuthorization()`.
- **Secrets** i secret store / user-secrets — **aldrig** i koden eller checkad-in appsettings. Typad config via **Options-mönstret** med `ValidateOnStart()`.
- **CORS** låst till kända origins (aldrig `AllowAnyOrigin` i prod). **HTTPS** påtvingat med **HSTS**. **Säkerhetsheaders satta:** `Strict-Transport-Security`, `Content-Security-Policy`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`.

### API
- Konsekventa **REST-konventioner** (verb, plural-substantiv, rätt statuskoder).
- **API-versioning från start** (`/api/v1/...`) så kundens integrationer inte bryts.
- **Swagger/OpenAPI** med JWT-stöd som levande dokumentation.

### Kvalitet
- **Tester** är ett krav: AAA-mönster, namn `Metod_Scenario_FörväntatResultat`. Unit-tester för handlers/validatorer/domänlogik. **Arkitekturtester** (NetArchTest) som skyddar lagergränserna.
- **Naming:** `PascalCase` (typer/metoder), `_camelCase` (privata fält), `IXxx` (interfaces), suffix `Async`. `nullable` på. `record` för immutabla typer. En klass per fil.
- **`.editorconfig`** incheckad styr formatering och analyzers konsekvent över maskiner och IDE:er. `dotnet format` + analyzers varningsfritt.
- **CI** kör build + alla tester vid varje push; bygget faller om tester eller linting failar.

---

## 🟢 FRONTEND (React) — måste från start

### Struktur & komponenter
- **TypeScript överallt** — `"strict": true` i `tsconfig.json` är påtvingat från start, undvik `any`. **Organisera per feature** (`features/orders/...`), publik export via `index.ts`.
- Endast **funktionskomponenter**, små och fokuserade (ett ansvar). Separera presentation (tar props) från container (logik/data).
- **Custom hooks** (`useXxx`) för återanvändbar logik — ett ansvar per hook, lever i `hooks/` nära sin feature eller i `src/hooks/` om delad. Exporteras via `index.ts`.
- **Path aliases** — använd `@/` istället för relativa `../../`-importer. Konfigurera i `tsconfig.json` och `vite.config.ts` från start.

### Props, state & events
- **Props är read-only** och alltid typade. Mutera aldrig props. Union-typer + defaultvärden.
- **State muteras aldrig direkt** (spread/map/filter). Funktionsformen `setX(prev => ...)`. **Härled** värden under render istället för att dubbellagra. Håll state lokalt; lyft upp endast vid delning.
- **Global state-strategi** — beslutsträd: server state → TanStack Query, URL-state → router, lokal UI → `useState`/`useReducer`, delad UI-state (tema, auth-kontext, modal) → Context sparsamt. Undvik Redux/Zustand utan att behovet är verkligt påvisat.
- **Events:** handlers heter `handleXxx`, props `onXxx`. Skicka funktions**referens**, inte anrop. Typa events. `preventDefault()` på formulär.
- **useEffect** bara för sidoeffekter — alla dependencies i arrayen, alltid cleanup, ingen härledning av renderbara värden. `eslint-plugin-react-hooks` aktivt.
- **Villkorlig rendering:** hantera alla tillstånd (loading/error/empty/data), tidig return, `&&` bara med riktiga booleans.
- **Listor:** stabil unik `key` (riktigt ID, aldrig index vid dynamiska listor).

### Datalager & navigation
- **Server-state via TanStack Query** (cache/refetch/mutationer) — inte manuell `useEffect`-fetch. Konsekventa `queryKey`, invalidera vid mutation.
- **Routing** med TanStack Router: typsäkra routes, nästlade layouter, **skyddade routes** (via `beforeLoad`) redirectar oinloggade, 404-route (`notFoundComponent`) finns.
- **Centraliserat API-lager** (axios-instans) med interceptors för auth-token och 401-hantering på ett ställe. API-URL från env-variabel.
- **Token-lagring** — spara aldrig JWT i `localStorage` (XSS-sårbart). Föredra en memory-variabel (nollställs vid sidladdning) eller `httpOnly`-cookie om backend stödjer det.
- **Miljövariabler** — Vite kräver `VITE_`-prefix för att exponera variabler i klienten (`import.meta.env.VITE_API_URL`). Typa dem i `src/vite-env.d.ts`. Lägg aldrig hemligheter i FE-env — allt bundlas in i klientkoden och är publikt.
- **Forms** med React Hook Form + **Zod** (validering + typ från samma schema). Tydliga felmeddelanden. Validera **alltid** även på backend.

### Prestanda
- **Mät innan du optimerar.** Använd React DevTools Profiler för att hitta faktiska flaskhalsar — optimera inte i förväg.
- **`React.memo`** på komponenter som renderas ofta med oförändrade props. **`useMemo`** för dyra beräkningar. **`useCallback`** för funktioner som skickas som props till memoiserade barn.
- **Kod-splitting på route-nivå** via `React.lazy` + `Suspense` — varje route laddas på begäran, inte allt vid start.

### Robusthet & kvalitet
- **Error boundary** kring appen/sektioner — ingen vit skärm vid krasch, logga felet, vänligt fallback-UI.
- **Tester** (Vitest + React Testing Library) utifrån användarens perspektiv (roller/text). Kritiska flöden täcks E2E (Playwright).
- **Tillgänglighet (a11y):** semantisk HTML, kopplade `label`, tangentbordsnavigering, kontrast, `aria-label` på ikonknappar. Ofta ett lag-/upphandlingskrav.
- **Naming:** komponenter `PascalCase`, hooks `useXxx`, booleans `is/has/can`. ESLint + Prettier varningsfritt.
- **Incheckad lint/format-config:** ESLint- och Prettier-config (och delad `.editorconfig`) ligger i repo så alla får exakt samma regler — inte bara lokala IDE-inställningar.

---

## 📁 Repo-uppstart & dokumentation (måste från start)

- **README.md** i varje repo: vad projektet är, förutsättningar, hur man installerar, kör (BE + FE), kör tester, samt vilka miljövariabler som krävs. Första filen en ny utvecklare (eller agent) läser.
- **`docs/PROJEKT-HANDOFF.md`** hålls aktuell — projektstatus, beslut, öppna frågor, nästa steg (se §0 p.14). Läses först varje ny session.
- **`.env.example`** checkas in med alla nödvändiga variabelnamn (utan värden). Riktiga `.env`/secrets checkas **aldrig** in.
- **Konsekvent formatering** via incheckad `.editorconfig` + Prettier/ESLint (FE) och `dotnet format` (BE) — samma regler oavsett maskin.

---

## 🔒 SÄKERHETSBASLINJE (tvärgående — måste från start)

Eftersom systemet säljs till kund och ofta granskas i revision/upphandling:

- **AuthN/AuthZ** korrekt (se backend ovan). Principen om **minsta behörighet**.
- **Lösenord hashas** (ASP.NET Identity / BCrypt) — aldrig i klartext. Refresh tokens för sessioner.
- **All input valideras** server-side. Parametriserade queries / EF → ingen SQL-injection.
- **Secrets** aldrig i repo. Konfiguration per miljö.
- **XSS:** lita på Reacts default-escaping. Använd **inte** `dangerouslySetInnerHTML` på otillförlitlig data. Sanera om det måste användas.
- **CSRF:** vid cookie-baserad auth — använd anti-forgery tokens / SameSite-cookies.
- **Kryptering:** TLS i transit (HTTPS överallt). Kryptera känsliga fält i vila (PII, hemligheter).
- **Audit logging:** logga vem som gjorde vad och när för känsliga åtgärder (skapa/ändra/ta bort, inloggning, behörighetsändringar). Spårbart och oföränderligt.
- **GDPR/PII:** identifiera personuppgifter, ha laglig grund, stöd gallring och "rätt att bli glömd", logga inte PII i klartext.
- **Dependency-scanning** i CI (`dotnet list package --vulnerable`, `npm audit`) — kända sårbarheter är vanligaste incidenten.
- **Inga interna fel/stack traces** läcker till slutanvändare.

> Mer avancerade säkerhetsåtgärder (fältkryptering i detalj, SAST/DAST, account lockout, secrets-rotation, rate limiting, pen-test) finns i [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md) — inför vid behov.

---

## ✅ Definition of Done (varje feature)

- [ ] Följer Clean Architecture & feature-struktur.
- [ ] Input validerad (FluentValidation / Zod) på rätt sida(or).
- [ ] Fel hanteras (ProblemDetails / error boundary) — inget läcker till användaren.
- [ ] Auth/behörighet på endpoints som kräver det.
- [ ] Schemaändringar via incheckad EF-migration (ingen manuell DB-ändring).
- [ ] Strukturerad loggning på plats; correlation-ID följer requesten; inga secrets/PII i loggar.
- [ ] Unit-tester (+ ev. integration/E2E) skrivna och gröna.
- [ ] Alla tillstånd i UI hanterade (loading/error/empty/data).
- [ ] Inga secrets i koden; naming-konventioner följda; linting varningsfri.
- [ ] Känsliga åtgärder audit-loggade; PII hanterad enligt GDPR-baslinjen.
- [ ] README/.env.example uppdaterade om uppstart eller miljövariabler ändrats.
- [ ] Commit följer Conventional Commits; PR öppnad (review/merge görs av människa).
