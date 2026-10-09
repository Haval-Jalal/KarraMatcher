# Staging-miljö — en hostad sandlåda att testa i (`#645`)

> **Varför:** en driftsatt ändring ska gå att prova **innan** den når pilotens riktiga familjer,
> och **utan** att röra deras data. Demo-inloggningen (fast kod `424242` + demo-barn) hör aldrig
> hemma i Production (§KM.1, säkerhetsgranskning `#611`) — men i en isolerad staging-miljö är den
> precis rätt verktyg. Grinden [`DemoAccessGuard`](../backend/src/KarraMatcher.Api/Features/Auth/DemoAccessGuard.cs)
> släpper igenom miljön `Staging`, men **aldrig** `Production`.

Staging är en **egen, hostad kopia** av både backend och frontend som spårar den långlivade
**`staging`-grenen** (prod spårar `main`). Du testar genom att öppna en staging-URL i webbläsaren —
även på mobilen, inget behöver köras lokalt.

| | spårar gren | backend | frontend | databas |
|---|---|---|---|---|
| **prod** | `main` | `karramatcher-api` (Render) | prod-Vercel | prod Neon |
| **staging** | `staging` | `karramatcher-api-staging` (Render) | staging-Vercel | **egen** Neon-branch |

Nyckeln som gör det här rent: frontendens Vercel-config är **kod** ([`frontend/vercel.ts`](../frontend/vercel.ts)),
inte statisk JSON. API-proxyns mål läses ur miljövariabeln **`KARRA_API_URL`**, så *samma* fil pekar
på prod eller staging beroende på vilket Vercel-projekt som bygger den. Därför är filerna identiska på
`staging` och `main`, och promotion-mergen blir konfliktfri (§KM.11).

---

## Engångsuppsättning

### 1. Neon — egen databas (branch)

Staging får **aldrig** peka på prod-databasen.

1. Neon → projektet → **Branches → New branch** (t.ex. `staging`).
2. Kopiera dess anslutningssträng (den med `?sslmode=require`).

### 2. Git — skapa `staging`-grenen

```bash
git checkout main && git pull
git checkout -b staging && git push -u origin staging
```

Både Render- och Vercel-staging deployar härifrån. Den lever kvar; du mergar `main` in i den när du
vill ta med senaste prod-läget.

### 3. Render — backend-tjänsten

Blueprinten finns i [`render.yaml`](../render.yaml) (tjänsten `karramatcher-api-staging`, `branch: staging`).

- **Blueprint:** Render → **New → Blueprint** → peka på repot → skapa `karramatcher-api-staging`.
- **Manuellt:** New → Web Service → Docker, `dockerfilePath: ./backend/Dockerfile`, `dockerContext: .`,
  Frankfurt, Free, **branch `staging`**, health `/health`.

Env-vars (dashboarden, `sync: false`):

| Nyckel | Värde |
|--------|-------|
| `ConnectionStrings__Default` | **Neon staging-branchens** sträng (steg 1) |
| `Auth__SigningKey` | En **egen** nyckel, ≥32 tecken — **inte** samma som prod |
| `Email__ApiKey` | Resend-nyckel (demon använder `424242`, så mejl triggas sällan) |
| `Chat__EncryptionKey` | Egen 32-byte base64 (`openssl rand -base64 32`) |
| `DemoSeed__AdminEmail` | En adress du når (t.ex. `du+admin@…`) |
| `DemoSeed__GuardianEmail` | En adress du når (t.ex. `du+foralder@…`) |

Resten (`DemoSeed__Enabled=true`, `Database__SeedOnStartup=true`, migrering, `ASPNETCORE_ENVIRONMENT=Staging`)
sätts av `render.yaml`. Notera staging-Render-URL:en, t.ex. `https://karramatcher-api-staging.onrender.com`.

### 4. Vercel — staging-frontend

1. **Prod-projektet (engång):** sätt env-varen **`KARRA_API_URL`** = prod-backend
   (`https://karramatcher-api.onrender.com`) på ditt befintliga Vercel-projekt. *(Behövs strikt inte —
   `vercel.ts` faller tillbaka på prod-URL:en om den saknas — men sätt den så beteendet är uttryckligt.)*
2. **Nytt staging-projekt:** Vercel → **Add New → Project** → samma repo → **Root Directory `frontend`**,
   **Production Branch `staging`**. Sätt env-varen **`KARRA_API_URL`** = **staging**-backend
   (`https://karramatcher-api-staging.onrender.com`).

> ⚠️ Staging-projektet **måste** sätta sin egen `KARRA_API_URL`. Glöms den pratar staging-frontenden
> med **prod**-backend (fallbacken). Ingen dataskada, men då testar du inte staging.

---

## Så testar du

Öppna **staging-Vercel-URL:en** (t.ex. på mobilen) → **Logga in** → skriv in `DemoSeed__AdminEmail`
(eller förälder-adressen) → ange koden **`424242`** i stället för att vänta på mejl. Hela kedjan —
kallelse → svar per barn → samåkning → chatt — körs mot demo-data som ingen riktig familj berörs av.

`DemoSeed__Clear=true` (sätt tillfälligt, deploya en gång, ta bort) rensar demodatat om du vill börja om.

*Alternativ utan hostad frontend:* kör frontend lokalt mot staging-backend med
`$env:KARRA_API_PROXY="https://karramatcher-api-staging.onrender.com"; npm run dev`.

---

## Flödet: testa i staging → merga till prod

```
feature-branch  →  PR mot `staging`  →  merge  →  staging (backend + frontend) deployar
                                                   →  testa på staging-URL (mobil)
          →  när bra:  PR `staging` → `main`  →  merge  →  prod deployar
```

- Du **promotar** alltså genom att merga `staging` → `main`. Eftersom `vercel.ts` är env-driven är
  filerna identiska på båda grenarna → **inga konflikter**.
- Håll `staging` i takt med prod: merga `main` in i `staging` då och då (t.ex. efter en prod-release).

---

## Viktigt

- **Prod är orört och säkert som standard.** `vercel.ts` faller tillbaka på prod-URL:en om
  `KARRA_API_URL` saknas, så prod beter sig identiskt som förr även innan varen satts. Och demo-grinden
  blockerar `DemoSeed:Enabled` i Production oavsett.
- **Egna hemligheter på staging.** Särskilt `Auth__SigningKey` — en läckt staging-nyckel ska aldrig ge
  tokens som duger i prod.
- **Render-URL:en bor nu i Vercel-varen `KARRA_API_URL`**, inte i `vercel.ts`-filen (§KM.11: ett ställe,
  men env, inte config-filen).
- **Gratisnivå:** staging-tjänsterna somnar vid inaktivitet. Första anropet efter en tyst stund tar
  ~50 s. Väntat för en testmiljö.
