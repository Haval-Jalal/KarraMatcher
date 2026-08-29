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

*Fylls i under M0, när lösningen och frontenden finns. Behåll den här rubriken uppdaterad — den är det
första en ny utvecklare (eller agent) läser efter handoff-filen.*

**Förutsättningar:** .NET SDK (senaste LTS) · Node 20+ · PostgreSQL · git · GitHub CLI

```bash
# Backend
cd backend
dotnet restore
dotnet ef database update --project src/KarraMatcher.Infrastructure --startup-project src/KarraMatcher.Api
dotnet run --project src/KarraMatcher.Api

# Frontend
cd frontend
npm install
npm run dev
```

**Tester**

```bash
cd backend  && dotnet test
cd frontend && npm run test && npm run lint && npx tsc --noEmit
```

**Miljövariabler:** se `.env.example` i respektive del. Riktiga `.env`-filer checkas aldrig in.

**Aktivera git-hooken en gång per klon** — den blockerar direktpush till `main`:

```bash
git config core.hooksPath .githooks
```

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
