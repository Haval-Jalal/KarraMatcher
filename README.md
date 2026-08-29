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
| `mallar/` | Ursprungsmallarna, orörda referenskopior. **Ändras aldrig** |

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
│   └─ MVP-PLAN.md            milstolpar
├─ backend/                   .NET-lösningen
├─ frontend/                  React + Vite (PWA)
└─ mallar/                    ursprungsmallar, referens
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
dotnet run --project src/KarraMatcher.Api      # svarar på http://localhost:5xxx/
```

Databasen kopplas in i M0-issue #6; tills dess startar API:t utan.

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
