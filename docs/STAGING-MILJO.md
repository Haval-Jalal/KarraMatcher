# Staging-miljö — en sandlåda att testa i (`#645`)

> **Varför:** en driftsatt ändring ska gå att prova **utan** att röra pilotens riktiga data.
> Demo-inloggningen (fast kod `424242` + demo-barn) hör aldrig hemma i Production bredvid riktiga
> familjer (§KM.1, säkerhetsgranskning `#611`) — men i en isolerad staging-miljö är den precis
> rätt verktyg. Grinden [`DemoAccessGuard`](../backend/src/KarraMatcher.Api/Features/Auth/DemoAccessGuard.cs)
> släpper igenom miljön `Staging`, men **aldrig** `Production`.

Staging kör **samma image** som prod (samma `backend/Dockerfile`, autodeploy från `main`), men i
miljön `Staging` och mot en **egen Neon-databas**. Blueprinten finns i
[`render.yaml`](../render.yaml) som tjänsten `karramatcher-api-staging`.

---

## Engångsuppsättning

### 1. Skapa en egen databas på Neon

Staging får **aldrig** peka på prod-databasen. Enklast är en **branch** i samma Neon-projekt:

1. Neon → projektet → **Branches** → **New branch** (t.ex. `staging`).
2. Kopiera dess anslutningssträng (den med `?sslmode=require`).

En branch delar inte data med prod efter att den skapats — skriv/radera fritt.

### 2. Skapa staging-tjänsten på Render

Blueprinten finns redan i `render.yaml`. Antingen:

- **Blueprint:** Render → **New** → **Blueprint** → peka på repot → den läser `render.yaml` och
  föreslår båda tjänsterna. Skapa `karramatcher-api-staging`.
- **Manuellt:** New → Web Service → repo → runtime Docker, `dockerfilePath: ./backend/Dockerfile`,
  `dockerContext: .`, region Frankfurt, plan Free, branch `main`, health check `/health`.

### 3. Sätt miljövariablerna (dashboarden, `sync: false`)

| Nyckel | Värde |
|--------|-------|
| `ConnectionStrings__Default` | **Neon staging-branchens** sträng (steg 1) |
| `Auth__SigningKey` | En **egen** nyckel, minst 32 tecken — **inte** samma som prod |
| `Email__ApiKey` | Resend-nyckel (demon använder `424242`, så mejl triggas sällan) |
| `Chat__EncryptionKey` | Egen 32-byte base64 (`openssl rand -base64 32`) |
| `DemoSeed__AdminEmail` | En adress du når (t.ex. `du+admin@…`) |
| `DemoSeed__GuardianEmail` | En adress du når (t.ex. `du+foralder@…`) |

Resten (`DemoSeed__Enabled=true`, `Database__SeedOnStartup=true`,
`Database__ApplyMigrationsOnStartup=true`, `ASPNETCORE_ENVIRONMENT=Staging`) sätts redan av
`render.yaml`.

### 4. Deploya

Render deployar automatiskt. Vid start kör den migrationerna och seedar baslaget **plus** de två
demokontona och demo-barnen (Liam J, Nova S, Elias B i *gul*-laget).

---

## Så testar du

- Öppna staging-URL:en, logga in som demo-admin eller demo-förälder, och mata in koden **`424242`**
  i stället för att vänta på ett mejl.
- Nu kan du köra hela kedjan — kallelse → svar per barn → samåkning → chatt — mot demo-data som
  ingen riktig familj berörs av.
- `DemoSeed__Clear=true` (sätt tillfälligt, deploya en gång, ta bort) rensar exakt det demodatat
  igen om du vill börja om.

---

## Viktigt

- **Prod är orört.** Demo-grinden blockerar `DemoSeed:Enabled` i Production; en osatt miljö
  defaultar till Production, så en kvarglömd flagga faller fortfarande uppstarten (Render behåller
  då den gamla containern — `render.yaml` beskriver det felläget).
- **Egen Auth-nyckel på staging.** En läckt staging-nyckel ska aldrig ge tokens som duger i prod.
- **Vill du testa _före_ en merge?** Peka staging-tjänstens `branch` på en långlivad `staging`-gren
  i stället för `main`, och pusha dit först. Standard här är `main` (samma som prod), så staging är
  en sandlåda för en ändring som redan landat — inte en förhandsgrind.
- **Gratisnivå:** staging-tjänsten somnar vid inaktivitet precis som prod. Första anropet efter en
  tyst stund tar ~50 s. Det är väntat för en testmiljö.
