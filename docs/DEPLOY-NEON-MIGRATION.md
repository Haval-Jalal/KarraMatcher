# Köra v2-migrationerna mot Neon

> **Läs det här innan v2 driftsätts.** Följ stegen uppifrån och ner. Den enda oåterkalleliga
> raden i hela migreringen är en `DROP TABLE` — därför är säkerhetskopian i **Steg 0** inte
> valfri.
>
> Regelverk: [`CLAUDE.md`](../CLAUDE.md) §KM.0 A2 · §KM.11 · Säkerhetschecklistan rad 11.

---

## Läget

Migrationerna är incheckade och körda lokalt, men **v2-uppsättningen har aldrig körts mot
Neon**. Det som väntar är migrationerna från och med `AddV2DomainModel` till och med
`AddAuditCorrelationId`:

| Migration | Vad | Not |
|---|---|---|
| `20260916150811_AddV2DomainModel` | Klubbar, åldersgrupper, lag, barn, vårdnadshavare (§KM.1) | additiv |
| `20260916191914_AddInvitations` | Inbjudningar (§KM.3) | additiv |
| `20260916195046_AddApplications` | Ansökningar (§KM.3) | additiv |
| `20260916202440_AddGuardianConsent` | Samtycke (§KM.6) | additiv |
| `20260918112040_RenameMatchToEvent` | `Matches` → `Events` (§KM.5/#198) | **`ALTER TABLE … RENAME` — data behålls** |
| `20260921081942_ReplaceAttendanceResponsesWithInvitations` | Ny kallelse per barn (#199) | **`DROP TABLE "AttendanceResponses"` — se nedan** |
| `20260921090214_NotificationPreferenceTypes` | Per-typ-notiser (#200) | döper om kolumner, behåller data |
| `20260921102925_AddChat` | Chatt (#201) | additiv |
| `20260923123103_AddAuditCorrelationId` | Correlation-id på audit-raden (#204) | additiv (nullbar kolumn) |

### Den enda raden som förlorar data

`ReplaceAttendanceResponsesWithInvitations` kör **`DROP TABLE "AttendanceResponses"`** och
skapar `AttendanceInvitations` i stället. Den gamla, antalsbaserade närvaron (v1) finns inte i
v2-modellen och går inte att föra över — den *ska* bort. Men det betyder att alla rader i
`AttendanceResponses` är borta efter migreringen. Har den tabellen aldrig fått riktig data
(t.ex. om närvaron aldrig driftsattes) är det ofarligt; annars är säkerhetskopian i Steg 0 det
som gör raden trygg. `RenameMatchToEvent` däremot **byter bara namn** på matchtabellen —
schemat och alla matcher följer med.

Allt annat är additivt (nya tabeller och kolumner) och rör ingen befintlig rad.

---

## Steg 0 — Säkerhetskopiera Neon (obligatoriskt)

Kör den logiska dumpen **innan** något schema ändras. Neons 6-timmars-PITR räcker inte som
enda skydd — en trasig fredagsmigration upptäckt lördag morgon ligger redan utanför fönstret
(se [`DATABAS-BACKUP.md`](./DATABAS-BACKUP.md)).

```bash
# Från repo-roten. Skriptet är dokumenterat i DATABAS-BACKUP.md.
pwsh scripts/Backup-Database.ps1
```

Ännu bättre på Neon: skapa en **branch** av databasen (Neon-konsolen → Branches → Create
branch). En branch är en momentan kopia du kan peka om till på sekunder om något går snett —
den är den snabbaste vägen tillbaka.

Fortsätt inte förrän dumpen ligger på disk **och** (helst) en Neon-branch finns.

---

## Steg 1 — Granska den exakta SQL:en

Migreringen körs som ett **idempotent** skript: varje migration är inlindad i en kontroll mot
`__EFMigrationsHistory`, så bara det som saknas körs, och skriptet är ofarligt att köra om.

Generera och läs igenom det innan du kör det mot Neon:

```bash
cd backend
dotnet ef migrations script --idempotent \
  --project src/KarraMatcher.Infrastructure \
  --startup-project src/KarraMatcher.Api \
  -o neon-migration.sql
```

Sök särskilt efter det enda destruktiva steget och bekräfta att det är det du väntar dig:

```bash
grep -n "DROP TABLE" neon-migration.sql   # ska bara vara "AttendanceResponses"
```

`neon-migration.sql` checkas **inte** in — den är en genererad artefakt och kan alltid tas
fram på nytt ur migrationerna, som är sanningskällan.

---

## Steg 2 — Applicera migrationerna

**Anslutningssträngen kommer alltid från miljön, aldrig från kod eller incheckad fil (§KM.11).**
Den är Neons `ConnectionStrings__Default` och sätts som en miljövariabel för det kommando eller
den tjänst som kör.

Välj **en** väg. Kör inte båda samtidigt.

### Väg A — kör det granskade skriptet (rekommenderas)

Mest kontroll: du kör exakt den SQL du nyss läste, från din maskin, medan du tittar. Kräver
`psql` (eller klistra in i Neons SQL-editor).

```bash
psql "<Neon-anslutningssträng>" -f backend/neon-migration.sql
```

Håll appens uppstartsmigrering **av** när du kör så här (`Database__ApplyMigrationsOnStartup`
lämnas `false` i Render), så att inget kör schemat en andra gång under en deploy.

### Väg B — låt backenden migrera vid uppstart

Enklare vid en vanlig deploy: appen kör `Database.MigrateAsync()` vid start när flaggan är på
(se `DatabaseInitializer`).

1. Sätt i Render: `Database__ApplyMigrationsOnStartup=true`.
2. Deploya backenden. Vid uppstart appliceras alla väntande migrationer, loggat som
   *"Applicerar databasmigrationer"* (EventId 2000).
3. När migreringen bekräftats i Steg 3 kan flaggan sättas tillbaka till `false` — ett schema
   ska inte ändras av bara farten vid nästa omstart. (Att låta den stå `true` är också
   försvarbart: `MigrateAsync` är en no-op när inget väntar. Välj medvetet.)

---

## Steg 3 — Verifiera

Kör mot Neon efteråt:

```sql
-- Alla migrationer registrerade (ska lista alla 25, inkl. de nio ovan).
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

-- v2-tabellerna finns, och den gamla närvaron är borta.
SELECT to_regclass('public."Events"'),               -- inte null
       to_regclass('public."AttendanceInvitations"'), -- inte null
       to_regclass('public."ChatMessages"'),          -- inte null
       to_regclass('public."AttendanceResponses"');   -- null (borttagen)

-- Correlation-id-kolumnen (#204) finns.
SELECT column_name FROM information_schema.columns
WHERE table_name = 'AuditEntries' AND column_name = 'CorrelationId';
```

Öppna sedan `/health/ready` mot den driftsatta backenden — den faller om databasen är onåbar
eller schemat inte stämmer (§KM.11).

---

## Steg 4 — Startdata (bara om Neon är tom)

Är detta en **ny, tom** Neon-databas behöver lag, spelplatser och det befintliga matchschemat
seedas en gång. Seedningen är idempotent (lägger bara till det som saknas):

- Render: `Database__SeedOnStartup=true`, deploya en gång, sätt sedan tillbaka till `false`.

Har Neon redan v1-data behövs det här inte — hoppa över det.

---

## Om något går fel

Ingen nedmigration körs som en del av det här. Går migreringen sönder halvvägs, eller visar
sig data saknas, är vägen tillbaka **att återställa från Steg 0** — peka om Neon-branchen, eller
läs in den logiska dumpen enligt [`DATABAS-BACKUP.md`](./DATABAS-BACKUP.md) → *Återställa*. Det
idempotenta skriptet är säkert att köra om när orsaken är åtgärdad.
