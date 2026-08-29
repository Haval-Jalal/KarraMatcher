# CLAUDE.md — Projektstandard för telefonapp (MÅSTE från start)

> **📌 Läs först:** [`docs/PROJEKT-HANDOFF.md`](./docs/PROJEKT-HANDOFF.md) — aktuell projektstatus,
> beslut, öppna frågor och nästa steg. Skriv ingen kod förrän ett issue är valt och godkänt.

> **Till agenten:** Detta är projektets regelverk för en **mobilapp på iOS + Android**. Följ det i **all** kod du genererar.
> Dessa element är obligatoriska från dag 1 — de utgör själva strukturen.
> Element som läggs till *vid behov* finns i [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md) —
> läs den filen också så du vet när de ska införas, men implementera dem inte i förväg (YAGNI).

**Teknikstack:**
- **Backend (BE)** = C# / .NET (senaste LTS), EF Core, MediatR, FluentValidation.
- **Frontend (FE / appen)** = React Native + **Expo** (senaste SDK), TypeScript, **Expo Router** (navigation),
  TanStack Query (server-state), React Hook Form + Zod (formulär), `expo-secure-store` (token-lagring).
- **Build & release** = **EAS Build / EAS Update** (OTA), App Store Connect (iOS) + Google Play Console (Android).

> **Plattformsmål:** Appen ska fungera till **100 % på både iOS och Android** från en gemensam kodbas.
> Allt som skrivs ska testas på **båda** plattformarna innan en feature anses klar (se DoD).

---

## Infrastruktur & Claude Code-konfiguration

Projektets `CLAUDE.md` är **projektlagret** i ett tvånivåsystem.

| Nivå | Plats | Vad den styr |
|------|-------|--------------|
| **Globalt** | `~/.claude/` | Kärnprinciper, agentteam, minne, hooks, inställningar — gäller *alla* projekt |
| **Projekt** | detta `CLAUDE.md` + `STANDARDER-VID-BEHOV.md` | Projektspecifika krav — har **företräde** vid konflikt |

Vad det globala lagret bidrar med:
- **Hooks (`~/.claude/settings.json`)** — `PreToolUse` blockerar direktpush till `main` och läsning av `*.secrets*`/`*.env*`-filer. `SessionStart` injicerar globala standarder vid sessionsstart.
- **Agenter (`~/.claude/agents/`)** — `reviewer`, `security_reviewer`, `debugger` delegeras vid rätt tillfälle (se §0).
- **Minne (`~/.claude/memory/`)** — rättelser och beslut sparas och gäller nästa session.
- **MCP-servrar** — GitHub-integration (om konfigurerad) ger åtkomst till issues, PRs och projektboard.
- **Skills** — `/code-review`, `/security-review` m.fl. laddas vid behov.
- **`settings.local.json`** — maskinspecifika val, checkas **aldrig** in i git.

> **Hook-principen:** allt du annars måste säga *"kom ihåg att…"* varje session → gör det till en hook. Hooks körs av verktyget, inte AI:n.

---

## 0. Så här arbetar du (process — gäller alltid)

1. **Plan först.** Presentera en kort plan och invänta godkännande innan du skriver mycket kod.
2. **Bygg i vertical slices** — en feature hela vägen (domän → handler → validator → API → app-skärm → tester) innan nästa.
3. **Tester per steg.** Skriv tester med koden och kör dem innan du går vidare.
4. **Litet och fokuserat.** Små klasser/komponenter, ett ansvar (SRP). Bryt ut tidigt.
5. **YAGNI.** Lägg inte till element från `STANDARDER-VID-BEHOV.md` förrän behovet faktiskt finns.
6. **Konsekvens > smarthet.** Följ befintliga mönster i kodbasen.
7. **Branch per ändring — aldrig direktpush till `main`.** Skapa **alltid** en egen branch *innan* du ändrar något (`feature/<kort-namn>`, `fix/...`, `docs/...`). Sammanslagning sker via PR (se punkt 8).
8. **Review och merge av PR är ALLTID manuellt (människa).** Claude/agenten får **absolut inte** granska, godkänna eller merga en PR — och får **inte heller fråga** om det är okej. Agentens tillåtna steg: skapa branch → ändra → committa → **pusha** branchen (och får öppna PR). Review/approve/merge görs alltid av en människa.
9. **Projektboard uppdateras alltid — utan att bli påmind.**
   - Innan första kodraden: flytta issue till **`In Progress`**.
   - När PR öppnas: flytta issue till **`In Review`**.
   - När PR mergad och issue stängs: flytta till **`Done`** och stäng issue (`gh issue close`).
10. **Branch-hygien mellan issues.** Innan ny issue: `git checkout main && git pull` (i både BE och app-repo), radera lokal branch. Börja aldrig en ny feature från en gammal branch.
11. **Kör lint och bygge lokalt innan commit.**
    - App: `npm run lint` (ESLint + Prettier, `--max-warnings 0`), `npx tsc --noEmit` (typkoll).
    - BE: `dotnet build` (varningsfritt) + `dotnet test` + `dotnet format`.
12. **Välj nästa issue från boarden.** Ta issues ur `Ready`-kolumnen — aldrig direkt ur Backlog utan godkännande av människa. Läs plan-/roadmap-dokument för MVP vs post-MVP.
13. **Verifiera BE-kontrakt innan FE-feature startas.** Kräver en app-skärm ett API-anrop — kontrollera att endpointen finns (rätt metod, URL, request/response-form) innan app-branchen skapas. Saknas något: bygg BE-gapet först.
14. **Uppdatera PROJEKT-HANDOFF.md när issues eller milstolpar stängs.** Det är första filen som läses i varje session.
15. **Dessa procesregler gäller i båda repon.** CLAUDE.md finns i både BE och app-repo. Ändringar i §0 speglas i båda.
16. **Aktivera Plan mode för icke-triviala uppgifter** (`/plan`) — arkitektur, nytt mönster, flera lager eller infrastruktur. Visa planen för godkännande innan kod.
17. **Delegera till specialistagenter.** Kodgranskning → `reviewer`. Säkerhet → `security_reviewer`. Rotorsak → `debugger`.
18. **Spara rättelser i memory direkt** när en regel ska gälla framåt.
19. **Commit-konvention (Conventional Commits).** `typ(scope): beskrivning` — `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`. Imperativ ton, rubrik ≤ 72 tecken. Referera issue (`#12`) när det finns.

---

## 🔵 BACKEND (C# / .NET) — måste från start

### Arkitektur & struktur
- **Clean Architecture.** Lager: `Domain → Application → Infrastructure → Api`. Beroenden pekar **alltid inåt**.
- `Domain` har **noll** ramverksberoenden. Affärsregler i domänen, inte i controllers.
- Interfaces definieras i `Application`, implementeras i `Infrastructure`.
- **Organisera per feature/modul** (`Features/Orders/...`), inte per teknisk typ.
- **Dependency Injection** via konstruktor. Varje lager har en `AddXxx`-extension. Rätt livstid (`Scoped` för DbContext).

### Use cases & data
- **CQRS + MediatR.** Commands ändrar state, Queries läser. En handler per use case. Controllers tunna (`_mediator.Send`).
- **DTOs (records)** för all in/utdata — exponera **aldrig** entiteter i API:t. Mapping på ett ställe.
- **Repository-interfaces i Application**, EF-implementation i Infrastructure. `AsNoTracking()` för read-only. Async + `CancellationToken` genomgående.

### Databas & migrations
- **EF Core Migrations från start.** Alla schemaändringar via migrations — **aldrig** manuellt i databasen.
- **Migrations checkas in** och körs kontrollerat. **Redigera aldrig** en migration som körts i delad/produktionsmiljö — skapa en ny.

### Validering & fel
- **FluentValidation** — en validator per command/query, körs via `ValidationBehavior`.
- **Global exception-middleware** → **ProblemDetails (RFC 7807)** med rätt statuskod. Inga stack traces till klient.

### Loggning, drift & observability
- **Strukturerad loggning** (Serilog) med konsekventa nivåer — aldrig `Console.WriteLine`.
- **Correlation-/request-ID** per request, propageras genom loggar (och till push/externa anrop).
- **Health checks** — `/health` (liveness) och `/health/ready` (readiness inkl. DB) från start.
- **Logga aldrig** secrets, tokens eller PII i klartext.

### Säkerhet (se även Säkerhetsbaslinje)
- **AuthN/AuthZ** från start: JWT med validering av issuer/audience/lifetime/signing key. **Policy-baserad** auktorisering. `UseAuthentication()` före `UseAuthorization()`.
- **Objektnivå-auktorisering (mot IDOR).** Kontrollera **alltid** att den inloggade användaren äger/får se den specifika resursen — inte bara att hen är inloggad. En `GET /orders/{id}` måste verifiera att ordern tillhör användaren. Detta är den vanligaste och allvarligaste API-sårbarheten (OWASP API #1) — bygg in det i varje handler, inte som eftertanke.
- **Refresh tokens med rotation** — mobilsessioner är långlivade; en stulen/utgången access-token ska bytas säkert. Indragning (revocation) av refresh-tokens stöds. Vid misstänkt återanvändning av en redan använd refresh-token: ogiltigförklara hela token-familjen.
- **Lösenordspolicy & kontoåterställning.** Minsta längd/styrka, kontroll mot kända läckta lösenord (t.ex. HaveIBeenPwned-range-API) om egen inloggning. E-postverifiering vid registrering. Säker återställning via tidsbegränsad engångstoken — aldrig lösenord i klartext via mejl.
- **MFA-beredskap.** Strukturera auth så att tvåfaktor (TOTP/SMS/passkeys) kan läggas till utan omskrivning — även om MFA införs först vid behov (se STANDARDER-VID-BEHOV).
- **Secrets** i secret store / user-secrets — **aldrig** i kod eller incheckad appsettings. Typad config via Options-mönstret med `ValidateOnStart()`.
- **CORS** låst till kända origins. **HTTPS** påtvingat med **HSTS**. Säkerhetsheaders satta.
- **Skydda utgående anrop (SSRF).** Om backend hämtar URL:er/resurser som påverkas av användarindata — validera mot en allowlist, blockera interna IP-intervall. Verifiera webhook-signaturer på inkommande webhooks.

### API — extra viktigt för mobil
- Konsekventa **REST-konventioner** (verb, plural-substantiv, rätt statuskoder).
- **API-versioning från start** (`/api/v1/...`). **Kritiskt för mobil:** användare uppdaterar inte appen direkt — gamla appversioner måste fungera mot API:t under en övergångsperiod. Bryt aldrig ett kontrakt utan ny version.
- **Kompakta payloads.** Mobilnät är långsamma/dyra — returnera bara fält appen behöver, paginera listor (se STANDARDER-VID-BEHOV).
- **Minsta-version-endpoint (tvingad uppdatering).** Exponera t.ex. `GET /api/v1/app-config` som returnerar `minSupportedVersion` + `latestVersion`. Appen kontrollerar vid start och kan tvinga uppdatering ("blocking update") när ett brytande API-byte krävt det, eller mjukt uppmana. Utan detta kan en gammal app i naturen krascha mot nytt API.
- **Maintenance-läge.** API:t kan signalera underhållsläge så appen visar en vänlig "vi är strax tillbaka"-skärm i stället för obegripliga fel.
- **Swagger/OpenAPI** med JWT-stöd som levande dokumentation.

### Push-notiser (BE-sida)
- Integration mot **APNs (iOS)** och **FCM (Android)** — eller via Expo Push API. Enhets-tokens registreras/avregistreras via endpoints och lagras per användare/enhet.
- Skicka notiser från bakgrundsjobb (se STANDARDER-VID-BEHOV), aldrig synkront i en request-tråd.

### Kvalitet
- **Tester** är ett krav: AAA-mönster, namn `Metod_Scenario_FörväntatResultat`. Unit-tester för handlers/validatorer/domänlogik. **Arkitekturtester** (NetArchTest) som skyddar lagergränserna.
- **Naming:** `PascalCase`, `_camelCase` (privata fält), `IXxx`, suffix `Async`. `nullable` på. `record` för immutabla typer. En klass per fil.
- **`.editorconfig`** incheckad. `dotnet format` + analyzers varningsfritt.
- **CI** kör build + tester vid varje push; bygget faller om tester/linting failar.

---

## 🟢 FRONTEND / APPEN (React Native + Expo) — måste från start

### Struktur & komponenter
- **TypeScript överallt** — `"strict": true` påtvingat, undvik `any`. **Organisera per feature** (`features/orders/...`), publik export via `index.ts`.
- Endast **funktionskomponenter**, små och fokuserade (ett ansvar). Separera presentation (props) från container (logik/data).
- **Custom hooks** (`useXxx`) för återanvändbar logik — ett ansvar per hook.
- **Path aliases** — `@/` istället för `../../`. Konfigurera i `tsconfig.json` + `babel.config.js` (babel-plugin-module-resolver).
- **Native UI-primitiver** — använd `View`, `Text`, `Pressable`, `FlatList` m.fl. (inte DOM/`div`/`span`). Styling via `StyleSheet` eller valt UI-bibliotek — bestäm **ett** och håll dig till det.

### Navigation
- **Expo Router** (fil-baserad routing). Typsäkra routes, nästlade layouter (stack/tabs/modal).
- **Skyddade routes** — oinloggade redirectas till login (kontrollera auth i en layout/`_layout`).
- **Deep linking / universal links** konfigureras tidigt om appen ska öppnas via länk eller push.

### Props, state & events
- **Props read-only** och alltid typade. Mutera aldrig props.
- **State muteras aldrig direkt** (spread/map/filter). Funktionsformen `setX(prev => ...)`. **Härled** värden under render istället för att dubbellagra.
- **Global state-strategi** — beslutsträd: server state → TanStack Query, navigations-state → Expo Router, lokal UI → `useState`/`useReducer`, delad UI-state (tema, auth) → Context sparsamt. Undvik Redux/Zustand utan påvisat behov.
- **Events:** handlers `handleXxx`, props `onXxx`. Skicka funktions**referens**. Typa events.
- **useEffect** bara för sidoeffekter — alla dependencies i arrayen, alltid cleanup. `eslint-plugin-react-hooks` aktivt.
- **Villkorlig rendering:** hantera alla tillstånd (loading/error/empty/data), tidig return.
- **Listor:** använd `FlatList`/`SectionList` (virtualiserade) för dynamiska listor — inte `.map()` i en `ScrollView`. Stabil unik `key` (riktigt ID, aldrig index).

### Datalager & nätverk
- **Server-state via TanStack Query** (cache/refetch/mutationer) — inte manuell `useEffect`-fetch. Konsekventa `queryKey`, invalidera vid mutation.
- **Centraliserat API-lager** (en axios/fetch-instans) med interceptors för auth-token och 401-hantering på ett ställe. API-URL från env (per app-variant: dev/staging/prod).
- **Nätverksresiliens (offline-medvetenhet):** mobilnät tappar uppkoppling. Hantera offline-tillstånd synligt för användaren, retrya med backoff, och låt TanStack Query återhämta vid återkomst. (Full offline-först-synk: se STANDARDER-VID-BEHOV.)
- **Forms** med React Hook Form + **Zod** (validering + typ från samma schema). Tydliga felmeddelanden. Validera **alltid** även på backend.

### Säkerhet på enheten (kritiskt — ersätter web-reglerna)
- **Token-lagring:** spara JWT/refresh-token **endast** i `expo-secure-store` (Keychain på iOS / Keystore på Android, OS-krypterat). **Aldrig** i `AsyncStorage`, global variabel som persisteras, eller i klartext.
- **Inga hemligheter i appkoden.** Allt som bundlas in i appen är publikt och kan dekompileras — inga API-nycklar till tredjepart med känslig behörighet, inga signeringshemligheter. Sådant bor i backend.
- **Biometrisk inloggning** (`expo-local-authentication`, Face ID / fingeravtryck) som skydd för känsliga flöden där det passar.
- **Auto-utloggning vid inaktivitet.** Känsliga appar loggar ut (eller kräver ny biometrik) efter en period av inaktivitet och vid behov när appen legat i bakgrunden länge.
- **Behörigheter (permissions)** begärs *just-in-time* med tydlig förklaring (kamera, plats, notiser) — aldrig allt vid start. Hantera nekad behörighet utan att appen kraschar.
- **Skydda känsliga skärmar:** dölj innehåll i app-växlaren (`expo-screen-capture` / `FLAG_SECURE` på Android, blur/overlay vid backgrounding på iOS) för skärmar med känslig data; överväg att blockera skärmdump där det krävs.
- **Urklipp & tangentbord:** lägg aldrig lösenord/tokens i urklipp; använd `secureTextEntry`/`textContentType` på känsliga fält så de utesluts från tangentbordscache och autofyll-historik.
- **Exkludera secrets från molnbackup.** Se till att token-lagring inte hamnar i iCloud/Android Auto Backup (`expo-secure-store` hanterar detta korrekt — verifiera vid egen lagring).
- **Validera deep links & push-payloads.** Lita aldrig på data från en länk/notis — verifiera och auktorisera server-side innan en åtgärd utförs. En deep link får navigera, men inte ensam utföra känsliga operationer.
- **Säker lokal lagring av appdata.** Cachad PII i SQLite/filsystem ska skyddas; lagra inte mer känslig data lokalt än nödvändigt och rensa vid utloggning.
- **Certificate pinning** och **root-/jailbreak-detektering** övervägs för känsliga appar (se STANDARDER-VID-BEHOV).

### Miljö & konfiguration
- **App-varianter** (dev / staging / prod) via `app.config.ts` + EAS-profiler. API-URL och miljöberoende värden injiceras per variant — aldrig hårdkodade.
- **Inga hemligheter i klient-env.** Allt i appens config bundlas in och är publikt.

### Plattform & UX (100 % på båda)
- **Testa på både iOS och Android** — visuellt och funktionellt. En feature är inte klar förrän den fungerar på båda (se DoD).
- **Safe areas** respekteras (`react-native-safe-area-context`) — notch, hörn, statusrad, hemknapp-indikator.
- **Plattformskännsla:** följ iOS- och Android-konventioner där de skiljer sig (tillbaka-gest, hårdvaru-bakåtknapp på Android, datum-/tidsväljare). Använd `Platform.select` vid behov.
- **Tillgänglighet (a11y):** `accessibilityLabel`/`accessibilityRole` på interaktiva element, testa med **VoiceOver (iOS)** och **TalkBack (Android)**, tillräcklig kontrast och träffyta (≥44pt).

### Prestanda
- **Mät innan du optimerar** (React DevTools Profiler, Flipper/Hermes-profiler).
- **`React.memo`/`useMemo`/`useCallback`** först när du mätt onödiga omrenderingar.
- **`FlatList`** för långa listor (virtualisering, `keyExtractor`, undvik tunga `renderItem`).
- **Bilder optimeras** (rätt storlek, `expo-image` med caching). Håll startup-tiden låg.

### Robusthet & kvalitet
- **Error boundary** kring appen/sektioner — ingen kraschskärm, logga felet, vänligt fallback-UI.
- **Crash- & felrapportering** (t.ex. Sentry) från ett tidigt skede — annars ser du inte fel ute på riktiga enheter. Skicka **aldrig** PII/tokens till felrapporteringen — maskera/scrubba.
- **Produktanalys (integritetsmedveten).** Mät kärnflöden (onboarding, aktivering, retention) för att kunna förbättra appen — men minimera data, respektera samtycke (App Tracking Transparency på iOS) och deklarera i privacy labels. Spåra aldrig utan grund.
- **Force-update-koll vid start** mot backendens minsta-version-endpoint (se BE/API) — visa blockerande eller mjuk uppdateringsuppmaning.
- **Tester** (Jest + React Native Testing Library) utifrån användarens perspektiv (roller/text). Kritiska flöden täcks E2E (Maestro eller Detox). Testa även på **fysisk enhet** för hårdvara/behörigheter — inte bara emulator.
- **Naming:** komponenter `PascalCase`, hooks `useXxx`, booleans `is/has/can`. ESLint + Prettier varningsfritt.
- **Incheckad lint/format-config** (ESLint, Prettier, `.editorconfig`) ligger i repo — samma regler för alla.

---

## 📦 Build, release & app stores (måste hanteras tidigt)

- **EAS Build** bygger app-binärer för iOS och Android i molnet — konfigurera `eas.json` med profiler (development/preview/production).
- **EAS Update (OTA)** för JS-ändringar utan ny store-release — men native-ändringar och SDK-uppgraderingar kräver ny binär.
- **Signering:** iOS via App Store Connect (certifikat/provisioning hanteras av EAS), Android via uppladdningsnyckel/Play App Signing. Signeringshemligheter checkas **aldrig** in.
- **App store-compliance från start (inte efteråt):**
  - **iOS:** Privacy Nutrition Labels (vilken data samlas/delas), åldersgräns, App Tracking Transparency om spårning sker.
  - **Android:** Data Safety-formulär, åldersgräns, deklarerade behörigheter motiverade.
  - **Båda:** synlig integritetspolicy, kontoborttagning i appen om konton finns (store-krav), inga otillåtna behörigheter.
- **Versionering:** semantisk app-version + byggnummer som ökar per release. Stötta gamla appversioner mot API:t (se BE API-versioning).

---

## 🔒 SÄKERHETSBASLINJE (tvärgående — måste från start)

- **AuthN/AuthZ** korrekt. Principen om **minsta behörighet**. **Objektnivå-auktorisering** på varje resurs (mot IDOR).
- **Lösenord hashas** (ASP.NET Identity / BCrypt) — aldrig i klartext. **Refresh tokens med rotation** för mobilsessioner. Lösenordspolicy + säker återställning.
- **All input valideras** server-side (klient-validering är bara UX). Parametriserade queries / EF → ingen SQL-injection.
- **Secrets** aldrig i repo eller i appbinär. Konfiguration per miljö. Inga hemligheter i klient-bundlad kod.
- **Transport:** TLS/HTTPS överallt (inga klartext-anrop). Certificate pinning vid behov (känsliga appar).
- **På enheten:** känsliga tokens i Keychain/Keystore (`expo-secure-store`), aldrig i klartext-lagring eller molnbackup. Biometriskt skydd och auto-utloggning där det passar. Känsliga skärmar skyddade i app-växlaren.
- **Audit logging:** logga vem/vad/när för känsliga åtgärder (skapa/ändra/ta bort, inloggning, behörighetsändringar). Oföränderligt. Övervaka/larma på säkerhetshändelser (upprepade inloggningsfel m.m.).
- **GDPR/PII:** identifiera personuppgifter, ha laglig grund, stöd gallring och "rätt att bli glömd", logga inte PII i klartext. Konto- och databorttagning i appen (även store-krav). Dataminimering — samla bara det som behövs.
- **Dependency-hygien:** scanning i CI (`dotnet list package --vulnerable`, `npm audit`), automatiska uppdaterings-PR:ar (Dependabot/Renovate), och **Expo SDK-uppgraderingar i tid** (utdaterad SDK tappar store-kompatibilitet och säkerhetsfixar).
- **Inga interna fel/stack traces** läcker till slutanvändaren.

> Fördjupningar (certificate pinning, root-/jailbreak-detektering, anti-tampering, MFA, WebView-härdning, fältkryptering, account lockout, SAST/DAST, secrets-rotation, pen-test, full offline-synk, DB-backup/DR) finns i [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md) — inför vid behov. En auditerbar checklista finns i [`SAKERHET-CHECKLISTA.md`](./SAKERHET-CHECKLISTA.md).

---

## ✅ Definition of Done (varje feature)

- [ ] Följer Clean Architecture & feature-struktur (BE) och feature-struktur (app).
- [ ] Input validerad (FluentValidation / Zod) på rätt sida(or).
- [ ] Fel hanteras (ProblemDetails / error boundary) — inget läcker till användaren.
- [ ] Auth/behörighet på endpoints som kräver det — inkl. **objektnivå-auktorisering** (användaren äger resursen, ej bara inloggad).
- [ ] Schemaändringar via incheckad EF-migration (ingen manuell DB-ändring).
- [ ] Strukturerad loggning; correlation-ID följer requesten; inga secrets/PII i loggar eller felrapportering.
- [ ] Tokens lagras säkert på enheten (`expo-secure-store`) — aldrig i klartext eller molnbackup.
- [ ] Känsliga skärmar skyddade (app-växlare/skärmdump) och känsliga fält använder `secureTextEntry`.
- [ ] **Testad och fungerar på BÅDE iOS och Android** (funktionellt + visuellt, inkl. safe areas) — fysisk enhet vid hårdvara/behörigheter.
- [ ] Alla tillstånd i UI hanterade (loading/error/empty/data + offline).
- [ ] A11y: interaktiva element märkta, testat med VoiceOver/TalkBack på kritiska flöden.
- [ ] Unit-tester (+ ev. E2E) skrivna och gröna.
- [ ] Inga secrets i koden/appbinären; naming-konventioner följda; linting + typkoll varningsfri.
- [ ] Känsliga åtgärder audit-loggade; PII hanterad enligt GDPR-baslinjen.
- [ ] README/.env.example/app-config uppdaterade om uppstart eller miljövariabler ändrats.
- [ ] Commit följer Conventional Commits; PR öppnad (review/merge görs av människa).
