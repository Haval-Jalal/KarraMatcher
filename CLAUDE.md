# CLAUDE.md — Projektstandard för Kärra Matcher (MÅSTE från start)

> **📌 Läs först:** [`docs/PROJEKT-HANDOFF.md`](./docs/PROJEKT-HANDOFF.md) — aktuell projektstatus,
> beslut, öppna frågor och nästa steg. Skriv ingen kod förrän ett issue är valt ur `Ready` och godkänt av människa.

> **Till agenten:** Detta är projektets regelverk. Följ det i **all** kod du genererar.
> Elementen här är obligatoriska från dag 1 — de utgör själva strukturen.
> Element som läggs till *vid behov* finns i [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md) —
> läs den filen också så du vet när de ska införas, men implementera dem inte i förväg (YAGNI).
> Släppgrinden är [`SAKERHET-CHECKLISTA.md`](./SAKERHET-CHECKLISTA.md).
> Produktens VAD och VARFÖR finns i [`SPEC.md`](./SPEC.md).

**Teknikstack:**
- **Backend (BE)** = C# / .NET (senaste LTS), EF Core, MediatR, FluentValidation, PostgreSQL (Npgsql).
- **Frontend (FE)** = React + TypeScript (Vite), TanStack Router, TanStack Query, React Hook Form + Zod.
- **Klient** = **PWA** (installerbar på hemskärmen, offline-läsbart schema). Ingen app store.
- **Repo** = **monorepo** med `backend/` och `frontend/`. Ett issue, en branch, en PR — även när ändringen rör båda.
- **Drift** = FE på **Vercel**, BE som Docker-container på **Render**, databas på **Neon**. Allt på fria nivåer.
  Vercel rewriter `/api/*` till Render, så klienten ser en enda origin. Se **§KM.11**.
  *(Samma uppsättning som carcheck.se, som redan är i drift.)*

**Ursprung:** Regelverket är en anpassning av mallen `mallar/StrukturBackendFrontend/`.
Avvikelser från mallen är samlade och motiverade i **§KM.0** — inför inga andra avvikelser utan att skriva in dem där.

---

## Infrastruktur & Claude Code-konfiguration

Projektets `CLAUDE.md` är **projektlagret** i ett tvånivåsystem.

| Nivå | Plats | Vad den styr |
|------|-------|--------------|
| **Globalt** | `~/.claude/` | Kärnprinciper, agentteam, minne, hooks, inställningar — gäller *alla* projekt |
| **Projekt** | detta `CLAUDE.md` + `STANDARDER-VID-BEHOV.md` | Projektspecifika krav — har **företräde** vid konflikt |

Vad det globala lagret bidrar med:
- **Hooks (`~/.claude/settings.json`)** — `PreToolUse` blockerar direktpush till `main` och läsning av `*.secrets*`/`*.env*`-filer (förstärker §0 p.7). `SessionStart` injicerar globala standarder.
- **Agenter (`~/.claude/agents/`)** — `reviewer`, `security_reviewer`, `debugger` delegeras vid rätt tillfälle (se §0 p.17).
- **Minne (`~/.claude/memory/`)** — rättelser och beslut sparas och gäller nästa session.
- **MCP-servrar** — GitHub-integration ger åtkomst till issues, PRs och projektboard.
- **Skills** — `/code-review`, `/security-review` m.fl. laddas vid behov.
- **`settings.local.json`** — maskinspecifika val, checkas **aldrig** in i git.
- **`effortLevel`** — `"high"` för arkitektur och felsökning, `"medium"` för rutinändringar.

> **Hook-principen:** allt du annars måste säga *"kom ihåg att…"* varje session → gör det till en hook. Hooks körs av verktyget, inte AI:n.

---

## 0. Så här arbetar du (process — gäller alltid)

1. **Plan först.** Presentera en kort plan och invänta godkännande innan du skriver mycket kod.
2. **Bygg i vertical slices** — en feature hela vägen (domän → handler → validator → API → frontend → tester) innan nästa.
3. **Tester per steg.** Skriv tester med koden och kör dem innan du går vidare.
4. **Litet och fokuserat.** Små klasser/komponenter, ett ansvar (SRP). Bryt ut tidigt.
5. **YAGNI.** Lägg inte till element från `STANDARDER-VID-BEHOV.md` förrän behovet faktiskt finns.
6. **Konsekvens > smarthet.** Följ befintliga mönster i kodbasen.
7. **Branch per ändring — aldrig direktpush till `main`.** Skapa **alltid** en egen branch *innan* du ändrar något (`feature/<kort-namn>`, `fix/...`, `docs/...`). Sammanslagning sker via PR (se punkt 8).
8. **Review och merge av PR är ALLTID manuellt (människa).** Agenten får **absolut inte** granska, godkänna eller merga en PR — och får **inte heller fråga** om det är okej. Agentens tillåtna steg: skapa branch → ändra → committa → **pusha** branchen (och får öppna PR). Review, approve och merge görs alltid av en människa.
9. **Projektboard uppdateras alltid — utan att bli påmind.**
   - Innan första kodraden: flytta issue till **`In Progress`**.
   - När PR öppnas: flytta issue till **`In Review`**.
   - När PR mergad och issue stängs: flytta till **`Done`** och stäng issue (`gh issue close`).
10. **Branch-hygien mellan issues.** Innan ny issue: `git checkout main && git pull`, radera lokal branch. Börja aldrig en ny feature från en gammal branch.
11. **Kör lint och bygge lokalt innan commit.**
    - FE: `npm run lint` (ESLint + Prettier, `--max-warnings 0`) + `npx tsc --noEmit`.
    - BE: `dotnet build` (varningsfritt) + `dotnet test` + `dotnet format`.
12. **Välj nästa issue från boarden.** Ta issues ur `Ready` — aldrig direkt ur Backlog utan godkännande av människa. Läs [`docs/MVP-PLAN.md`](./docs/MVP-PLAN.md) för att avgöra om ett issue hör till MVP eller post-MVP.
13. **Verifiera BE-kontrakt innan FE-feature startas.** Kräver en FE-vy ett API-anrop — kontrollera att endpointen finns (rätt metod, URL, request/response-form) innan FE-arbetet börjar. Saknas något: bygg BE-gapet först, i egen commit inom samma PR (monorepo) eller egen PR om ändringen är stor.
14. **Uppdatera `docs/PROJEKT-HANDOFF.md` när issues eller milstolpar stängs.** Det är första filen som läses i varje session.
15. **Regelverket gäller båda sidor.** `backend/CLAUDE.md` och `frontend/CLAUDE.md` är pekare hit — lägg aldrig motstridiga regler där.
16. **Aktivera Plan mode för icke-triviala uppgifter** (`/plan`) — arkitektur, nytt mönster, flera lager eller infrastruktur. Visa planen för godkännande innan kod.
17. **Delegera till specialistagenter.** Kodgranskning → `reviewer`. Säkerhet → `security_reviewer`. Rotorsak → `debugger`.
18. **Spara rättelser i memory direkt** när en regel ska gälla framåt.
19. **Commit-konvention (Conventional Commits).** `typ(scope): beskrivning` — `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`. Imperativ ton, rubrik ≤ 72 tecken. Referera issue (`#12`) när det finns. Scope är `be`, `fe`, `db`, `docs` eller featurenamn.

---

## §KM. PROJEKTSPECIFIKA REGLER (går före mallen)

> Kärra Matcher hanterar uppgifter om **barn under 13 år** och delas med ett hundratal föräldrar
> som inte är tekniska. Reglerna nedan följer av det och av besluten i [`SPEC.md`](./SPEC.md).
> De är **inte** valfria och de **väger tyngre** än motsvarande skrivning i mallen.

### §KM.0 Medvetna avvikelser från mallen

| # | Mallen säger | Vi gör | Motivering |
|---|--------------|--------|------------|
| A1 | Rate limiting = *vid behov* | **Baslinje från start** | Schema- och ICS-endpoints är publika och oautentiserade på öppet internet |
| A2 | DB-backup = *vid behov (inför tidigt)* | **Baslinje från start** | Ett tappat matchschema mitt i säsongen kostar fyra tränare en kväll var att skriva in på nytt |
| A3 | Tillgänglighet = "WCAG om upphandlingskrav" | **WCAG 2.1 AA som eget krav** | Mor- och farföräldrar och föräldrar med skärmläsare är verkliga användare |
| A4 | Auth krävs "från start" på API:t | **Publik läsning, autentiserad skrivning** | Produktbeslut: en förälder som bara vill se matchtiden ska aldrig mötas av inloggning |
| A5 | Push via APNs/FCM | **Web Push (VAPID)** | PWA, ingen app store. iOS kräver hemskärm-installation — kalenderfeeden är fallbacken |
| A6 | Två repon | **Monorepo** | Ensam utvecklare; mallen tillåter uttryckligen monorepo. En PR kan röra både BE och FE |
| A7 | Steg 0 = de fyra kommersiella filtren | **Omskrivna för ideell app** | Ingen betalar för appen; grinden mäter i stället om den faktiskt kommer att användas |
| A8 | All data i backend | **Barnstatistiken lagras enbart på enheten** | Spelarkortet är familjens egen sak. Data som aldrig når servern kan inte läcka från den — men den kan gå förlorad, se §KM.2 |

### §KM.1 Barn-PII — hårt tak på vad som får lagras

- **Tillåtet om ett barn:** förnamn (eller smeknamn), tröjnummer, lag-id, aktiv/inaktiv.
- **Förbjudet överallt** — databas, loggar, cache, felrapportering, analytics, ICS-feed, push-payload:
  efternamn, personnummer, födelsedatum, adress, telefonnummer, e-post, foto, hälsouppgifter, position.
- Ny kolumn som kan innehålla personuppgift får **inte** införas utan att beslutet skrivs in i
  `docs/PROJEKT-HANDOFF.md` under *Viktiga beslut* — i samma PR.
- Fritextfält som en användare kan skriva i (t.ex. samåkningsnotis) räknas som potentiell PII:
  de får inte loggas, inte indexeras och inte visas för fler än den avsedda mottagarkretsen.

### §KM.2 Barnets statistik lämnar aldrig enheten

Spelarkortet — matchresultat, mål, assist, spelade matcher, märken — är tänkt som något föräldern och
barnet fyller i tillsammans efter matchen. Den datan **lagras uteslutande i familjens egen telefon**.

- **Det finns ingen tabell, ingen entitet och ingen endpoint för barnstatistik på servern.**
  Backend kan inte läsa den, kan inte ta emot den och kan inte råka logga den — därför att den aldrig
  når dit. Detta är avsiktligt och är projektets starkaste integritetsskydd.
- **Förbjudet:** att skicka statistik, barnets namn eller spelarkortets innehåll i något API-anrop,
  i en push-payload, i felrapportering eller i telemetri. Ingen "synk-funktion vid behov" senare
  utan ett skrivet beslut i `docs/PROJEKT-HANDOFF.md`.
- **Testkrav:** ett arkitektur- eller integrationstest verifierar att ingen endpoint tar emot eller
  returnerar spelarstatistik. Införs en sådan endpoint av misstag ska bygget falla.
- **Konsekvens som måste hanteras, inte döljas:** byter föräldern telefon, rensar webbläsardata eller
  avinstallerar appen är statistiken borta. Därför gäller följande som funktionskrav, inte som extra:
  1. **Säkerhetskopiering är en förstaklassfunktion** — en kod som kan kopieras och klistras in igen,
     med en synlig uppmaning att spara den. Formatet är bakåtkompatibelt med `KARRA1.`.
  2. **`navigator.storage.persist()` begärs** så webbläsaren inte gallrar lagringen.
  3. **Installation på hemskärmen uppmuntras tydligt på iOS** — Safari kan annars rensa lagring för
     webbplatser som inte använts på en vecka, och den här appen används säsongsvis.
  4. Gränssnittet ska vara ärligt om var datan finns — samma ton som föregångarens
     *"Sparas bara på den här telefonen"*.
- Två vårdnadshavare med varsin telefon får varsin uppsättning statistik. Det är en följd av modellen
  och ska förklaras i gränssnittet, inte "lösas" med en server.
- `Player` finns på servern **enbart** som tränarens trupp för den vilande kallelsen (§KM.7).
  Barnet i spelarkortet är en lokal post och har ingen motsvarighet i databasen.

### §KM.3 Publik läsning, autentiserad skrivning

- **Anonymt tillåtet:** lagens matchlista, enskild match, spelplatser, ICS-feed, appens statiska innehåll,
  **att se lagets samåkningserbjudanden**, och att prenumerera på notiser om matchändringar.
- **Kräver inloggning:** allt som skriver — inklusive att lägga upp samåkning och att skicka en
  åkförfrågan — samt all tränarfunktion.
- **En gäst kan titta, men inte delta.** Knappar för att lägga upp eller begära skjuts visas för gäster
  med en uppmaning att logga in — de får aldrig ett tyst fel.
- Anonyma endpoints ska ha rate limiting (§KM.0 A1), får aldrig returnera personuppgifter,
  och ska vara cachebara utan att någon användares data blandas in.
- Auktorisering är **policy-baserad**: `Coach` (per lag), `Admin`, `Guardian` (per spelare).
  Rollkontroller hårdkodas aldrig i controllers.

### §KM.4 ICS-feeden

- Publik, oautentiserad, per lag. Innehåller **enbart** matchdata: lag, motståndare, tid, plats, adress, status.
- Aldrig barnnamn, aldrig närvaro, aldrig samåkning.
- Uppdaterad match måste öka `SEQUENCE` och inställd match sätta `STATUS:CANCELLED`, annars uppdateras
  inte föräldrarnas kalendrar.

### §KM.5 Tid och tidszon

- Lagring i **UTC** (`timestamptz`). Visning och inmatning i **Europe/Stockholm**.
- Konvertering sker på ett ställe i BE och ett ställe i FE — aldrig utspritt.
- **Testkrav:** minst ett testfall som passerar sommartidsskiftet i oktober (säsongen sträcker sig dit).

### §KM.6 Samtycke och radering

- **På servern** hamnar ett barns förnamn bara genom tränarens trupp (vilande kallelse, §KM.7).
  Innan en tränare lägger upp truppen ska samtyckesrutinen vara klar; accepterad version och tidsstämpel sparas.
- **Spelarkortet kräver inget samtycke från oss** — den datan lämnar aldrig telefonen (§KM.2). Däremot
  ska gränssnittet vara tydligt med var den finns och att den försvinner om telefonen byts utan säkerhetskopia.
- Radering av ett barn tar bort spelaren, dess närvarosvar och kopplingen — direkt, inte
  "markerad som raderad". Radering av ett konto tar bort kontot och allt som ägs av det, inklusive
  samåkningserbjudanden och förfrågningar. Spelarkortet raderas separat på enheten.
- Ingen tredjepartsspårning, ingen besöksanalys, inga externa skript i FE utöver väder (Open-Meteo)
  och utgående kartlänkar. Nya tredjeparter kräver beslut i handoff-filen.

### §KM.7 Feature flags — kallelsen är byggd men avstängd

- Närvaro/kallelse styrs av `Team.AttendanceEnabled`. Flaggan kontrolleras **serverside i varje handler**,
  inte bara genom att dölja knappar i FE.
- Är flaggan av returnerar endpointen `404`, och FE visar inte funktionen.

### §KM.8 Offline och PWA

- Schemat ska gå att läsa utan nät (service worker cachar lagets matcher och statiska tillgångar).
- v1 är **offline-medveten**, inte offline-först: skrivningar köas inte, användaren får tydligt besked.
  Full synk är ett `STANDARDER-VID-BEHOV`-element.
- Service worker får aldrig cacha auth-svar. Spelarkortet ligger i enhetens egen lagring, inte i sw-cachen.

### §KM.9 Språk

- **UI-text, felmeddelanden och dokumentation: svenska.**
- **Kod, identifierare, typnamn, kommentarer, commit-meddelanden, branchnamn, issues: engelska.**
- Inga svenska tecken i filnamn, tabellnamn eller API-fält.

### §KM.10 Loggning i det här projektet

- Aldrig i loggar: barnets namn, användarens e-post, push-endpoint, JWT, fritext från användare.
- Referera alltid till personer och barn med **id**, aldrig med namn.
- Audit-logg krävs för: skapa/ändra/ta bort match, ställa in match, ändra tränarroll, koppla eller
  ta bort barn, radera konto.

### §KM.11 Drift: Vercel och Render bakom en och samma origin

Uppsättningen är hämtad från carcheck.se, som redan kör den i produktion.

```
frontend/  →  Vercel (statisk SPA + edge-cache)
backend/   →  Render (Docker, ASP.NET på port 8080)
database   →  Neon Postgres
frontend/vercel.json:  /api/:path*  →  https://<render-url>/api/:path*
```

- **Databasen ligger på Neon, inte på Render.** Renders gratisdatabas upphör efter 30 dagar; Neons gör det inte. Familjernas statistik får inte ligga på något som självdör.
- **Klienten ser en enda origin.** Det medför tre bindande regler:
  1. **CORS öppnas inte upp.** Blir något ett CORS-fel är det för att någon anropat Render-URL:en direkt — fixa anropet, inte CORS-policyn.
  2. **Refresh-token-cookien är en förstapartscookie** — `HttpOnly`, `Secure`, `SameSite=Lax`. Det är hela poängen med proxyn och får inte offras.
  3. **Render-URL:en hårdkodas aldrig i frontend-koden.** Den finns på exakt ett ställe: `frontend/vercel.json`.
- **Kallstart är ett verkligt UX-problem.** Render free somnar efter ca 15 minuters tystnad och tar omkring 50 sekunder att vakna — och appen används mest lördag morgon, efter en tyst natt. Tre motåtgärder, alla obligatoriska:
  1. Publika GET-svar (lagets schema, matchdetalj, ICS-feeden) sätter `Cache-Control: public, s-maxage=…` så **Vercels edge svarar utan att väcka Render**. Det är den vanligaste sidvisningen i hela appen.
  2. Ett gratis uppetidsverktyg pingar `/health` med några minuters mellanrum.
  3. Tar ett anrop ändå lång tid ska UI:t säga det på svenska — aldrig en spinner som ser trasig ut.
- **Schemalagda jobb:** Vercel Hobby tillåter cron **en gång per dygn**, vilket räcker exakt för påminnelsen kvällen före match. Push vid matchändring är händelsestyrd och sker i requesten — den behöver ingen cron.
- **Vercel Hobby är endast för icke-kommersiell användning.** Appen får aldrig ta betalt eller visa reklam utan att driftfrågan omprövas.

### §KM.12 Samåkning — erbjudande, förfrågan, svar

Samåkning är appens enda funktion där föräldrar interagerar med varandra. Flödet är därför reglerat.

**Tillstånd**

```
Erbjudande:  Öppet ──▶ Fullt ──▶ Tillbakadraget
Förfrågan:   Väntar ──▶ Accepterad
                    └─▶ Nekad
                    └─▶ Återtagen (av den som frågade)
```

- **Lägga upp** kräver inloggning. Föraren anger **1–4 platser**, riktning (till, från eller båda),
  avgångsplats, avgångstid och en valfri notis.
- **Skicka förfrågan** kräver inloggning. Förfrågan anger antal platser (standard 1) och en valfri hälsning.
- **En gäst ser erbjudandena men kan varken lägga upp eller fråga** (§KM.3).
- **Endast den som lagt upp erbjudandet svarar.** Accept eller nekande — och **ett nekande kräver
  alltid ett meddelande**. Gränssnittet erbjuder färdiga formuleringar ("Ändrade planer, kan tyvärr
  inte köra", "Någon annan hann före") plus fritext. Ett tyst nej får inte förekomma; det är en
  granne man möter på planen nästa lördag.
- **Platsräkning:** bara **accepterade** förfrågningar förbrukar platser. En accept som skulle
  överskrida antalet platser blockeras server-side med ett begripligt fel — aldrig genom att bara
  dölja knappen.
- **Förfrågningar tillåts även när erbjudandet är fullt.** Det är avsiktligt: föraren ska kunna svara
  "någon annan hann före" i stället för att den som frågar möts av en död knapp. Fullt erbjudande
  märks tydligt i listan.
- **Notiser:** nytt erbjudande → till lagets prenumeranter. Ny förfrågan → till föraren.
  Svar → till den som frågade. Tillbakadraget erbjudande → till alla som accepterats.
- **Fritext är potentiell PII.** Den loggas aldrig, visas bara för de inblandade och lagets tränare,
  och **hela samåkningen för en match gallras 30 dagar efter matchen**.
- **Inga telefonnummer lagras av oss.** Överenskommelsen sker i meddelandefältet. Väljer en förälder
  att själv skriva sitt nummer i fritexten är det deras beslut — appen efterfrågar det aldrig,
  och gallringen ovan gäller ändå.

---

## 🔵 BACKEND (C# / .NET) — måste från start

### Arkitektur & struktur
- **Clean Architecture.** Lager: `Domain → Application → Infrastructure → Api`. Beroenden pekar **alltid inåt**.
- `Domain` har **noll** ramverksberoenden (ingen EF, ingen ASP.NET). Affärsregler bor i domänen, inte i controllers.
- Interfaces definieras i `Application`, implementeras i `Infrastructure`.
- **Organisera per feature/modul** (`Features/Matches/...`), inte per teknisk typ.
- **Dependency Injection** via konstruktor — inga `new` på beroenden. Varje lager har en `AddXxx`-extension. Rätt livstid (`Scoped` för DbContext).

**Lösningsstruktur:**

```
backend/
  KarraMatcher.sln
  src/
    KarraMatcher.Domain/
    KarraMatcher.Application/
    KarraMatcher.Infrastructure/
    KarraMatcher.Api/
  tests/
    KarraMatcher.Domain.Tests/
    KarraMatcher.Application.Tests/
    KarraMatcher.Architecture.Tests/
    KarraMatcher.Api.IntegrationTests/
```

### Use cases & data
- **CQRS + MediatR.** Commands ändrar state, Queries läser. En handler per use case. Controllers tunna (`_mediator.Send`).
- **DTOs (records)** för all in/utdata — exponera **aldrig** entiteter i API:t. Mapping på ett ställe.
- **Repository-interfaces i Application**, EF-implementation i Infrastructure. `AsNoTracking()` för read-only. Async + `CancellationToken` genomgående.

### Databas & migrations
- **EF Core Migrations från start.** Alla schemaändringar via migrations — **aldrig** manuellt i databasen.
- **Migrations checkas in** och körs kontrollerat. **Redigera aldrig** en migration som körts i delad/produktionsmiljö — skapa en ny.
- En migration per logisk schemaändring med beskrivande namn. Granska genererad SQL innan den körs mot en delad miljö.
- **Seed** av lag, spelplatser och det befintliga matchschemat är **idempotent** och körs kontrollerat.

### Validering & fel
- **FluentValidation** — en validator per command/query, körs via `ValidationBehavior`.
- **Global exception-middleware** → **ProblemDetails (RFC 7807)** med rätt statuskod. Inga stack traces till klient. Result-mönster för förväntade fel.

### Loggning, drift & observability
- **Strukturerad loggning** (Serilog) med konsekventa nivåer — aldrig `Console.WriteLine`.
- **Correlation-/request-ID** per request, propageras genom loggarna.
- **Health checks** — `/health` (liveness) och `/health/ready` (readiness inkl. DB) från start.
- **Logga aldrig** secrets, tokens eller PII i klartext — se §KM.10 för vad som är PII här.

### Säkerhet
- **AuthN/AuthZ** från start: JWT med validering av issuer/audience/lifetime/signing key. **Policy-baserad** auktorisering. `UseAuthentication()` före `UseAuthorization()`.
- **Objektnivå-auktorisering (mot IDOR)** i varje handler — se §KM.2 för projektets skärpta krav.
- **Refresh tokens med rotation** och återanvändnings-detektering. Sessioner i en PWA är långlivade.
- **Lösenordspolicy & kontoåterställning** om egen inloggning används: minsta längd/styrka, kontroll mot kända läckta lösenord, e-postverifiering, säker återställning via tidsbegränsad engångstoken.
- **Rate limiting från start** (§KM.0 A1) på publika endpoints och inloggning. `429` med `Retry-After`.
- **Secrets** i secret store / user-secrets — **aldrig** i kod eller incheckad appsettings. Typad config via Options-mönstret med `ValidateOnStart()`.
- **CORS** låst till kända origins (aldrig `AllowAnyOrigin` i prod). **HTTPS** påtvingat med **HSTS**. Säkerhetsheaders satta: `Strict-Transport-Security`, `Content-Security-Policy`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`.
- **Skydda utgående anrop (SSRF).** Väder-API:t anropas med fasta koordinater från vår egen databas — aldrig med en URL eller ett värde som kommer från användarindata.

### API
- Konsekventa **REST-konventioner** (verb, plural-substantiv, rätt statuskoder).
- **API-versioning från start** (`/api/v1/...`).
- **Kompakta payloads** — appen används på mobilnät vid fotbollsplaner med dålig täckning.
- **Swagger/OpenAPI** med JWT-stöd som levande dokumentation.

### Kvalitet
- **Tester** är ett krav: AAA-mönster, namn `Metod_Scenario_FörväntatResultat`. Unit-tester för handlers/validatorer/domänlogik. **Arkitekturtester** (NetArchTest) som skyddar lagergränserna.
- **Naming:** `PascalCase`, `_camelCase` (privata fält), `IXxx`, suffix `Async`. `nullable` på. `record` för immutabla typer. En klass per fil.
- **`.editorconfig`** incheckad. `dotnet format` + analyzers varningsfritt.
- **CI** kör build + tester vid varje push; bygget faller om tester eller linting failar.

---

## 🟢 FRONTEND (React + TypeScript, PWA) — måste från start

### Struktur & komponenter
- **TypeScript överallt** — `"strict": true` påtvingat, undvik `any`. **Organisera per feature** (`features/matches/...`), publik export via `index.ts`.
- Endast **funktionskomponenter**, små och fokuserade (ett ansvar). Separera presentation (props) från container (logik/data).
- **Custom hooks** (`useXxx`) för återanvändbar logik — ett ansvar per hook.
- **Path aliases** — `@/` istället för `../../`. Konfigurerat i `tsconfig.json` och `vite.config.ts` från start.

**Mappstruktur:**

```
frontend/
  public/            manifest.webmanifest, ikoner
  src/
    app/             router, layouter, providers
    features/        matches/ teams/ players/ stats/ carpool/ attendance/ admin/ auth/
    components/      delade presentationskomponenter
    hooks/           delade hooks
    lib/             api-klient, datum/tidszon, ics, push, storage
    styles/
```

### Props, state & events
- **Props read-only** och alltid typade. Mutera aldrig props.
- **State muteras aldrig direkt** (spread/map/filter). Funktionsformen `setX(prev => ...)`. **Härled** värden under render istället för att dubbellagra.
- **Global state-strategi** — server state → TanStack Query, URL-state → router, lokal UI → `useState`/`useReducer`, delad UI-state (tema, auth, valt lag) → Context sparsamt. Undvik Redux/Zustand utan påvisat behov.
- **Events:** handlers `handleXxx`, props `onXxx`. Skicka funktions**referens**. Typa events. `preventDefault()` på formulär.
- **useEffect** bara för sidoeffekter — alla dependencies i arrayen, alltid cleanup. `eslint-plugin-react-hooks` aktivt.
- **Villkorlig rendering:** hantera alla tillstånd (loading/error/empty/data **och offline**), tidig return.
- **Listor:** stabil unik `key` (riktigt ID, aldrig index).

### Datalager & navigation
- **Server-state via TanStack Query** — inte manuell `useEffect`-fetch. Konsekventa `queryKey`, invalidera vid mutation.
- **Routing** med TanStack Router: typsäkra routes, nästlade layouter, **skyddade routes** via `beforeLoad`, 404-route finns.
- **Centraliserat API-lager** med interceptors för auth-token och 401-hantering på ett ställe. API-URL från env.
- **Token-lagring** — JWT sparas **aldrig** i `localStorage`. Access-token i minnet, refresh-token i `httpOnly`-cookie från backend.
- **Miljövariabler** — Vite kräver `VITE_`-prefix. Typa i `src/vite-env.d.ts`. Lägg **aldrig** hemligheter i FE-env; allt bundlas in och är publikt.
- **Forms** med React Hook Form + **Zod**. Validera **alltid** även på backend.

### PWA
- `manifest.webmanifest` med namn, ikoner, `display: standalone`, temafärg.
- Service worker cachar appskal och lagets schema (§KM.8). Aldrig auth-svar.
- Tydlig uppdateringshantering: när en ny version finns ska användaren få veta det, inte fastna på en gammal.
- Installationstips visas diskret — särskilt på iOS, där push kräver hemskärm-installation.

### Prestanda
- **Mät innan du optimerar** (React DevTools Profiler, Lighthouse).
- **`React.memo`/`useMemo`/`useCallback`** först när du mätt onödiga omrenderingar.
- **Kod-splitting på route-nivå** via `React.lazy` + `Suspense`.

### Robusthet & kvalitet
- **Error boundary** kring appen/sektioner — ingen vit skärm, logga felet, vänligt fallback-UI.
- **Tester** (Vitest + React Testing Library) utifrån användarens perspektiv (roller/text). Kritiska flöden täcks E2E (Playwright).
- **Tillgänglighet: WCAG 2.1 AA** (§KM.0 A3) — semantisk HTML, kopplade `label`, tangentbordsnavigering, kontrast, `aria-label` på ikonknappar, synlig fokusmarkering, respekt för `prefers-reduced-motion`.
- **Naming:** komponenter `PascalCase`, hooks `useXxx`, booleans `is/has/can`. ESLint + Prettier varningsfritt.
- **Incheckad lint/format-config** (ESLint, Prettier, `.editorconfig`) i repo.

---

## 📁 Repo-uppstart & dokumentation (måste från start)

- **`README.md`** i roten: vad projektet är, förutsättningar, hur man installerar och kör BE + FE, hur man kör tester, vilka miljövariabler som krävs.
- **`docs/PROJEKT-HANDOFF.md`** hålls aktuell (se §0 p.14). Läses först varje ny session.
- **`docs/MVP-PLAN.md`** — milstolpar och vad som hör till MVP respektive post-MVP.
- **`.env.example`** checkas in med alla nödvändiga variabelnamn (utan värden). Riktiga `.env`/secrets checkas **aldrig** in.
- **Konsekvent formatering** via incheckad `.editorconfig` + Prettier/ESLint (FE) och `dotnet format` (BE).

---

## 🔒 SÄKERHETSBASLINJE (tvärgående — måste från start)

- **AuthN/AuthZ** korrekt. Principen om **minsta behörighet**. **Objektnivå-auktorisering** på varje resurs (§KM.2).
- **Lösenord hashas** (ASP.NET Identity / BCrypt) — aldrig i klartext. Refresh tokens med rotation.
- **All input valideras** server-side (klient-validering är bara UX). Parametriserade queries / EF → ingen SQL-injection.
- **Secrets** aldrig i repo eller i klient-bundlad kod. Konfiguration per miljö.
- **XSS:** lita på Reacts default-escaping. Använd **inte** `dangerouslySetInnerHTML` på otillförlitlig data.
- **CSRF:** refresh-token i cookie → anti-forgery token + `SameSite`.
- **Rate limiting** på publika endpoints och inloggning (§KM.0 A1).
- **Transport:** TLS/HTTPS överallt, HSTS.
- **Audit logging:** vem/vad/när för känsliga åtgärder (§KM.10). Oföränderligt.
- **GDPR/PII:** personuppgifter identifierade, laglig grund dokumenterad i SPEC, gallring och radering stödd, PII aldrig i klartext i loggar. **Barn-PII enligt §KM.1.**
- **DB-backup automatisk och testad återställning** (§KM.0 A2).
- **Dependency-scanning** i CI (`dotnet list package --vulnerable`, `npm audit`) + Dependabot/Renovate.
- **Inga interna fel/stack traces** läcker till slutanvändaren.

> Fördjupningar finns i [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md) — inför vid behov.
> Auditerbar grind: [`SAKERHET-CHECKLISTA.md`](./SAKERHET-CHECKLISTA.md).

---

## ✅ Definition of Done (varje feature)

- [ ] Följer Clean Architecture & feature-struktur.
- [ ] Input validerad (FluentValidation / Zod) på rätt sida(or).
- [ ] Fel hanteras (ProblemDetails / error boundary) — inget läcker till användaren.
- [ ] Auth/behörighet på endpoints som kräver det — inkl. **objektnivå-auktorisering**.
- [ ] **Rör featuren spelarkortet: ingen data lämnar enheten — verifierat att inget API-anrop, ingen push-payload och ingen felrapport innehåller den (§KM.2).**
- [ ] **Rör featuren samåkning: platsräkning och krav på meddelande vid nekande är kontrollerade server-side (§KM.12).**
- [ ] **Ingen ny PII-kolumn utan beslut infört i `docs/PROJEKT-HANDOFF.md` (§KM.1).**
- [ ] Schemaändringar via incheckad EF-migration (ingen manuell DB-ändring).
- [ ] Strukturerad loggning; correlation-ID följer requesten; inga secrets/PII i loggar (§KM.10).
- [ ] **Tider lagrade i UTC, visade i Europe/Stockholm; testfall över sommartidsskifte där det är relevant (§KM.5).**
- [ ] Alla tillstånd i UI hanterade (loading/error/empty/data **och offline**).
- [ ] **A11y: WCAG 2.1 AA — tangentbord, kontrast, märkta kontroller, synlig fokus.**
- [ ] **UI-text på svenska, kod och commits på engelska (§KM.9).**
- [ ] Unit-tester (+ ev. integration/E2E) skrivna och gröna.
- [ ] Inga secrets i koden; naming-konventioner följda; linting och typkoll varningsfria.
- [ ] Känsliga åtgärder audit-loggade.
- [ ] README/.env.example uppdaterade om uppstart eller miljövariabler ändrats.
- [ ] Commit följer Conventional Commits; PR öppnad (review/merge görs av människa).
