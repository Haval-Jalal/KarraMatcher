# Kärra Matcher

Matchschema, väder, vägbeskrivning, samåkning och ett spelarkort som stannar i telefonen — för Kärra P2016 — fyra lag, ett hundratal
föräldrar, och tränare som ska kunna ändra schemat på en halv minut.

Föräldern öppnar en länk och ser när och var nästa match spelas. Inget konto. Vill hen prenumerera på
lagets matcher i sin egen telefonkalender räcker ett klick, och efter det uppdaterar sig matcherna av sig
själva. Spelarkortet — där föräldern och barnet fyller i resultat, mål och assist efter matchen — sparas
enbart i den egna telefonen och kräver inte heller något konto. Konto behövs först för samåkning och för
tränarnas schemaverktyg.

**Status:** planering. Regelverk och spec är satta; ingen kod skriven än. Se
[`docs/PROJEKT-HANDOFF.md`](./docs/PROJEKT-HANDOFF.md).

---

## Läs i den här ordningen

| Fil | Roll |
|-----|------|
| [`docs/PROJEKT-HANDOFF.md`](./docs/PROJEKT-HANDOFF.md) | **Läses först varje session.** Status, beslut, öppna frågor, nästa steg |
| [`SPEC.md`](./SPEC.md) | **VAD & VARFÖR** — problemvalidering, omfattning, domänmodell, roller, API |
| [`CLAUDE.md`](./CLAUDE.md) | **HUR** — process, arkitektur, säkerhet, projektspecifika regler (§KM), Definition of Done |
| [`docs/MVP-PLAN.md`](./docs/MVP-PLAN.md) | Milstolpar M0–M8 och vad som hör till MVP respektive backlog |
| [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md) | Element som införs först när triggern uppfylls (YAGNI) |
| [`SAKERHET-CHECKLISTA.md`](./SAKERHET-CHECKLISTA.md) | Auditerbar releasegrind — bockas av före varje produktionssläpp |
| [`docs/DATABAS-BACKUP.md`](./docs/DATABAS-BACKUP.md) | **Läses när något gått fel.** Backuprutin och återställning, steg för steg |
| [`docs/DRIFTOVERVAKNING.md`](./docs/DRIFTOVERVAKNING.md) | Uppetidsövervakning, pingschema och Renders timbudget |

## Teknik

- **Backend:** C# / .NET (senaste LTS) · Clean Architecture · EF Core · MediatR · FluentValidation · PostgreSQL
- **Frontend:** React + TypeScript (Vite) · TanStack Router · TanStack Query · React Hook Form + Zod
- **Klient:** PWA — installerbar på hemskärmen, schemat läsbart offline. Ingen app store
- **Notiser:** Web Push (VAPID) samt ICS-kalenderfeed per lag
- **Repo:** monorepo — `backend/` och `frontend/`
- **Drift:** frontend på **Vercel**, backend som Docker-container på **Render**, databas på **Neon** — allt gratis

```
Webbläsare ──▶ Vercel (SPA + edge-cache)
                 │  vercel.json:  /api/:path*  ──▶  Render (.NET, Docker, :8080)
                 │                                        │
                 └── allt annat ──▶ index.html            └──▶ Neon Postgres
```

Rewriten gör att klienten ser **en enda origin**: ingen CORS-konfiguration behövs, och
refresh-token-cookien blir en förstapartscookie. Render-URL:en finns på exakt ett ställe —
`frontend/vercel.json`. Se `CLAUDE.md` §KM.11.

## Struktur

```
KarraMatcher/
├─ CLAUDE.md                  regelverket
├─ SPEC.md                    produktspecifikationen
├─ STANDARDER-VID-BEHOV.md    vilande element
├─ SAKERHET-CHECKLISTA.md     releasegrind
├─ docs/
│   ├─ PROJEKT-HANDOFF.md     levande status
│   ├─ MVP-PLAN.md            milstolpar
│   ├─ DATABAS-BACKUP.md      backuprutin och återställning
│   └─ DRIFTOVERVAKNING.md    uppetidsövervakning
├─ scripts/
│   └─ Backup-Database.ps1    dump och bevisad återställning
├─ backend/                   .NET-lösningen
└─ frontend/                  React + Vite (PWA)
```

## Kom igång

**Förutsättningar:** .NET SDK 10 (låses av `global.json`) · Node 20+ · git · GitHub CLI

**Aktivera git-hooken en gång per klon** — den blockerar direktpush till `main`:

```bash
git config core.hooksPath .githooks
```

**Backend**

```bash
cd backend
dotnet restore

# Anslutningssträngen krävs. Lokalt via user-secrets:
dotnet user-secrets --project src/KarraMatcher.Api set   "ConnectionStrings:Default" "Host=...;Database=...;Username=...;Password=..."

# Signeringsnyckeln for inloggningen. Minst 32 tecken, valideras vid start.
dotnet user-secrets --project src/KarraMatcher.Api set   "Auth:SigningKey" "$(openssl rand -base64 48)"

dotnet ef database update --project src/KarraMatcher.Infrastructure   --startup-project src/KarraMatcher.Api

dotnet run --project src/KarraMatcher.Api      # svarar på http://localhost:5xxx/
```

Databasen är **Neon Postgres** i EU-region. API:t vägrar starta utan anslutningssträng
— det är avsiktligt, så att en felkonfigurerad miljö upptäcks direkt i stället för
vid första databasanropet.

Lokalt körs migrationer och startdata automatiskt vid uppstart, styrt av
`appsettings.Development.json`. I drift är båda avstängda som standard och slås på
med `Database__ApplyMigrationsOnStartup` och `Database__SeedOnStartup` — att ändra ett
databasschema ska inte ske av bara farten för att någon startade appen mot fel databas.

**Startdata:** föreningen Kärra, åldersgruppen P2016 säsongen 2026, fyra lag, sju
spelplatser med koordinater och 25 matcher. Seeden är idempotent — den körs vid varje
driftsättning utan att dubblera något.

Två saker som är lätta att snäva på:

- **`dotnet ef` läser inte user-secrets.** Ange anslutningen uttryckligen när du kör
  migrationer: `dotnet ef database update --connection "<sträng>"`.
- **`dotnet run` tvingar Development** via `Properties/launchSettings.json`, oavsett vad
  `ASPNETCORE_ENVIRONMENT` säger. Vill du prova produktionsbeteende lokalt behövs
  `--no-launch-profile`. Render påverkas inte — containern startar binären direkt.

**Frontend**

```bash
cd frontend
npm install
npm run dev          # http://localhost:5173
```

**Kontroller före commit**

```bash
cd backend
dotnet build                          # ska vara varningsfritt
dotnet test                           # alla gröna
dotnet format --verify-no-changes     # inga formatdiffar

cd ../frontend
npm run typecheck                     # tsc -b, inga fel
npm test                              # alla gröna
npm run lint                          # ESLint, --max-warnings 0
npm run format:check                  # Prettier
```

`npm run format` skriver om filerna. Markdown är undantaget — dokumentationen är
handformaterad och delas med rotens dokument, som ligger utanför Prettiers räckvidd.

## Backendens uppbyggnad

```
backend/
├─ KarraMatcher.slnx              lösningsfil (.NET 10:s XML-format)
├─ Directory.Build.props          gemensamma bygginställningar
├─ src/
│   ├─ KarraMatcher.Domain/       affärsregler — noll ramverksberoenden
│   ├─ KarraMatcher.Application/  use cases, validering, interfaces
│   ├─ KarraMatcher.Infrastructure/  databas och externa tjänster
│   └─ KarraMatcher.Api/          controllers, middleware, DI
└─ tests/
    ├─ KarraMatcher.Domain.Tests/
    ├─ KarraMatcher.Application.Tests/
    ├─ KarraMatcher.Architecture.Tests/     bevakar lagergränserna
    └─ KarraMatcher.Api.Integration.Tests/
```

Beroenden pekar alltid inåt: `Api → Infrastructure → Application → Domain`.
Arkitekturtesterna läser de **deklarerade** referenserna i csproj-filerna, inte de kompilerade —
en oanvänd referens elideras av kompilatorn och skulle annars slinka igenom obemärkt.

## Frontendens uppbyggnad

```
frontend/
├─ vite.config.ts / vitest.config.ts   alias @/ speglas i båda och i tsconfig
└─ src/
    ├─ app/          router, providers, query-klient
    ├─ features/     en mapp per funktionsområde
    ├─ components/   delade presentationskomponenter
    ├─ hooks/        delade hooks
    ├─ lib/          api-klient, datum och tidszon, ics, push, storage
    └─ styles/
```

Server-state hanteras av TanStack Query, routing av TanStack Router — aldrig av `useEffect`-fetch.
Path alias `@/` är konfigurerat på **tre** ställen som måste ändras tillsammans:
`tsconfig.app.json`, `vite.config.ts` och `vitest.config.ts`.

## Driftsättning

**Backend → Render.** Tjänsten är definierad i [`render.yaml`](./render.yaml). Peka Render
på repot, välj *New Blueprint*, och sätt `ConnectionStrings__Default` i dashboarden —
den kommer aldrig från repot.

Imagen byggs från [`backend/Dockerfile`](./backend/Dockerfile) med **repo-roten som
byggkontext**, så att `global.json` och `.editorconfig` följer med. Containern bygger
därmed med samma SDK och samma analysregler som CI och din maskin.

```bash
# Bygg lokalt (från repo-roten, inte från backend/)
docker build -f backend/Dockerfile -t karramatcher-api .
docker run --rm -p 8080:8080   -e ConnectionStrings__Default="<sträng>"   karramatcher-api
```

Två val i imagen som är medvetna:

- **Debian-baserad runtime**, inte alpine eller chiseled. Appen behöver både ICU och
  tidszonsdata för `Europe/Stockholm` (§KM.5) — en bantad image utan dem skulle få
  `SwedishTime` att kasta redan vid uppstart.
- **Ingen `HEALTHCHECK` i imagen.** Render gör sin egen HTTP-kontroll mot `/health`.
  En `HEALTHCHECK` i imagen skulle bero på verktyg som inte finns i den slimmade
  runtime-imagen och rapportera unhealthy utan att någon märkte det.

## Edge-cachen — verifierad, inte antagen

Hela kallstartsförsvaret i §KM.11 vilar på att Vercels edge cachar svar den proxar vidare
till Render. Det är en extern rewrite, och att den cachas alls var länge ett antagande.

**Verifierat 2026-08-30 mot skarp drift:**

| Anrop | `X-Vercel-Cache` | Tid | `Rndr-Id` |
|---|---|---|---|
| 1 | `MISS` | 1,25 s | `16d71163-dc28-444b` |
| 2 | `HIT` | 0,11 s | `16d71163-dc28-444b` |

Beviset är inte tiden utan **`Rndr-Id`**. Render utfärdar ett nytt id per request, så ett
identiskt id på anrop två betyder att backend aldrig kontaktades — svaret kom från edge.

Två saker att känna till:

- **Vercel försvagar vår ETag.** En stark `"abc"` kommer ut som `W/"abc"`, eftersom edge kan
  komprimera svaret på vägen. Vår `If-None-Match`-tolkning hanterar `W/`-prefixet, så `304`
  fungerar hela vägen genom proxyn. Tas den hanteringen bort slutar villkorade anrop fungera
  i drift utan att något test i CI märker det.
- **`s-maxage` syns inte utåt.** Edge konsumerar direktivet och skickar vidare
  `public, max-age=0` till webbläsaren. Det är väntat och rätt.

Felsvar cachas inte: ett `404` kommer ut med `private, no-store` och `MISS`.

## Databasbackup

Två lager: Neons kontinuerliga historik (PITR) för de vanliga olyckorna, och logiska dumpar
för det osannolika fallet att Neon-projektet självt försvinner.

```powershell
./scripts/Backup-Database.ps1                 # dump
./scripts/Backup-Database.ps1 -VerifyRestore  # dump + bevisad återställning
```

Skriptet kräver bara Docker — PostgreSQLs klientverktyg körs i containrar. Det rör aldrig
källdatabasen, och anslutningssträngen läses ur `dotnet user-secrets`.

Rutinen, och vad du gör när något faktiskt gått fel, står i
[`docs/DATABAS-BACKUP.md`](./docs/DATABAS-BACKUP.md). Dumparna hamnar utanför repot och
checkas aldrig in — en dump innehåller barns förnamn så snart en trupp lagts upp (§KM.1).

## Arbetssätt

Projektet följer [`CLAUDE.md`](./CLAUDE.md) §0 till punkt och pricka:

- Ett issue åt gången, taget ur `Ready` på [projektboarden](https://github.com/users/Haval-Jalal/projects/7)
- Egen branch per ändring — **aldrig direktpush till `main`**
- **Review och merge av PR görs alltid av en människa**, aldrig av en agent
- Conventional Commits · lint och bygge lokalt före commit · handoff-filen uppdateras när ett issue stängs

## Integritet

Appen är byggd för att behandla så lite som möjligt om barn — helst ingenting.

**Spelarkortet lämnar aldrig telefonen.** Barnets namn, matchresultat, mål och assist sparas enbart i
familjens egen enhet. Det finns ingen tabell, ingen endpoint och ingen möjlighet för servern att läsa
det — inte ens för en administratör. Det kräver därför inget konto. Baksidan är att datan försvinner vid
telefonbyte utan säkerhetskopia, så backupkoden är en förstaklassfunktion och inte en extrafiness.

Med kallelsen avstängd behandlar servern alltså **inga uppgifter alls om barn**. Aktiveras den lagras
endast förnamn och tröjnummer, aldrig efternamn, personnummer, födelsedatum, adress eller foto.
Ingen spårning, ingen besöksanalys, all data inom EU. Detaljerna står i `CLAUDE.md` §KM.1, §KM.2 och §KM.6.
