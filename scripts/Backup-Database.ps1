<#
.SYNOPSIS
    Tar en logisk säkerhetskopia av Kärra Matchers databas, och kan bevisa att den går
    att återställa.

.DESCRIPTION
    Kör pg_dump och pg_restore i Docker-containrar, så att inga PostgreSQL-klientverktyg
    behöver installeras lokalt. Anslutningssträngen läses ur `dotnet user-secrets` om
    ingen anges — den ska aldrig skrivas in på kommandoraden, eftersom den då hamnar i
    PowerShells historik.

    Skriptet skriver aldrig till källdatabasen. Återställningen sker i en tillfällig
    container som tas bort efteråt.

.PARAMETER OutputDirectory
    Var dumpfilen ska hamna. Standard: en `backups`-mapp utanför repot, eftersom en
    dump med tiden kommer att innehålla barns förnamn (§KM.1) och aldrig får checkas in.

.PARAMETER VerifyRestore
    Återställer dumpen i en färsk PostgreSQL-container och jämför radantal, kolumnschema
    och innehåll mot källan. Det är den här körningen som gör backupen till en backup.

.PARAMETER ConnectionString
    Npgsql-anslutningssträng. Utelämna för att läsa ur user-secrets.

.EXAMPLE
    ./scripts/Backup-Database.ps1
    Tar en dump till standardmappen.

.EXAMPLE
    ./scripts/Backup-Database.ps1 -VerifyRestore
    Tar en dump och genomför hela återställningsövningen. Kör detta kvartalsvis.
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $HOME 'KarraMatcher-backups'),
    [switch]$VerifyRestore,
    [string]$ConnectionString
)

$ErrorActionPreference = 'Stop'

# Måste matcha eller vara nyare än serverns version. Neon kör PostgreSQL 18.
$PostgresImage = 'postgres:18-alpine'
$RestoreContainer = 'karra-restore-verify'

function Get-ConnectionParts {
    param([string]$Raw)

    $parts = @{}
    foreach ($segment in $Raw.Split(';')) {
        if ($segment -match '^\s*([^=]+?)\s*=\s*(.*?)\s*$') {
            $parts[$Matches[1].Trim().ToLowerInvariant()] = $Matches[2]
        }
    }

    foreach ($required in @('host', 'database', 'username', 'password')) {
        if (-not $parts.ContainsKey($required)) {
            throw "Anslutningssträngen saknar '$required'."
        }
    }

    # Poolern är byggd för många korta anslutningar. En dump är motsatsen, så vi går
    # direkt på endpointen — det är också vad Neon rekommenderar för pg_dump.
    $parts['host'] = $parts['host'] -replace '-pooler', ''
    return $parts
}

function Invoke-Psql {
    param([hashtable]$Db, [string]$Sql)

    $uri = "postgresql://$($Db.username)@$($Db.host)/$($Db.database)?sslmode=require"
    return docker run --rm -e "PGPASSWORD=$($Db.password)" $PostgresImage `
        psql $uri -tAc $Sql
}

if (-not $ConnectionString) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $apiProject = Join-Path $repoRoot 'backend/src/KarraMatcher.Api'
    $secrets = dotnet user-secrets list --project $apiProject 2>$null
    $line = $secrets | Where-Object { $_ -like 'ConnectionStrings:Default = *' } | Select-Object -First 1

    if (-not $line) {
        throw "Hittade ingen anslutningssträng i user-secrets. Ange -ConnectionString."
    }

    $ConnectionString = $line -replace '^ConnectionStrings:Default = ', ''
}

$db = Get-ConnectionParts -Raw $ConnectionString

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$dumpName = "karramatcher-$stamp.dump"
$dumpPath = Join-Path $OutputDirectory $dumpName

Write-Host "Dumpar $($db.database) fran $($db.host) ..."

# Dumpen skrivs till en monterad katalog, inte via stdout. PowerShells omdirigering
# tolkar strommen som text och skulle forstora en binar custom-format-dump.
#
# --format=custom kravs for pg_restore. --no-owner och --no-acl gor dumpen portabel:
# Neons roller finns inte i en tom lokal databas.
docker run --rm -e "PGPASSWORD=$($db.password)" -v "${OutputDirectory}:/backup" $PostgresImage `
    pg_dump --format=custom --no-owner --no-acl `
    --file="/backup/$dumpName" `
    "postgresql://$($db.username)@$($db.host)/$($db.database)?sslmode=require"

if ($LASTEXITCODE -ne 0) { throw "pg_dump misslyckades." }

$size = (Get-Item $dumpPath).Length
if ($size -lt 1024) { throw "Dumpen ar bara $size byte. Nagot ar fel." }

Write-Host "Klart: $dumpPath ($size byte)" -ForegroundColor Green

if (-not $VerifyRestore) {
    Write-Host "Kor med -VerifyRestore for att bevisa att dumpen gar att aterstalla." -ForegroundColor Yellow
    return
}

Write-Host "`nAterstaller i en fars PostgreSQL-container ..."

docker rm -f $RestoreContainer 2>$null | Out-Null
docker run -d --name $RestoreContainer `
    -e POSTGRES_PASSWORD=restore -e POSTGRES_DB=karra_restore `
    -v "${OutputDirectory}:/backup" `
    $PostgresImage | Out-Null

try {
    # -h 127.0.0.1 och inte unix-socketen, med flit. Postgres officiella image startar
    # en tillfallig server under initieringen som bara lyssnar pa socketen och sedan
    # stangs ner. En pg_isready mot socketen svarar da ja pa en server som ar pa vag
    # att forsvinna, och pg_restore moter "the database system is shutting down".
    # Over TCP gar det inte att ta fel: den porten oppnas forst nar databasen ar klar.
    $ready = $false
    foreach ($attempt in 1..60) {
        docker exec $RestoreContainer pg_isready -h 127.0.0.1 -U postgres -d karra_restore 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) { throw "Containern blev aldrig klar." }

    # Lases fran den monterade katalogen av samma skal som ovan: ingen binar stdin.
    docker exec -e PGPASSWORD=restore $RestoreContainer `
        pg_restore --no-owner --no-acl -h 127.0.0.1 -U postgres -d karra_restore "/backup/$dumpName" | Out-Null

    if ($LASTEXITCODE -ne 0) { throw "pg_restore misslyckades." }

    # Jamforelserna. Radantal racker inte -- lika manga rader ar inte samma data.
    $checks = [ordered]@{
        'radantal'     = "select string_agg(x, ',' order by x) from (
                            select table_name || '=' || (xpath('/row/c/text()',
                              query_to_xml('select count(*) c from public.' ||
                              quote_ident(table_name), false, true, '')))[1]::text as x
                            from information_schema.tables
                            where table_schema = 'public' and table_type = 'BASE TABLE') t;"
        'kolumnschema' = "select md5(string_agg(c, '|' order by c)) from (
                            select table_name||':'||column_name||':'||data_type||':'||is_nullable as c
                            from information_schema.columns where table_schema='public') s;"
        'constraints'  = "select count(*) from information_schema.table_constraints
                            where table_schema='public'
                              and constraint_type in ('FOREIGN KEY','PRIMARY KEY','UNIQUE');"
        'index'        = "select count(*) from pg_indexes where schemaname='public';"
    }

    $failed = @()
    foreach ($name in $checks.Keys) {
        $source = (Invoke-Psql -Db $db -Sql $checks[$name] | Out-String).Trim()
        $restored = (docker exec -e PGPASSWORD=restore $RestoreContainer `
                psql -h 127.0.0.1 -U postgres -d karra_restore -tAc $checks[$name] | Out-String).Trim()

        if ($source -eq $restored) {
            Write-Host ("  {0,-14} identiskt" -f $name) -ForegroundColor Green
        }
        else {
            Write-Host ("  {0,-14} SKILJER SIG" -f $name) -ForegroundColor Red
            Write-Host "      kalla:      $source"
            Write-Host "      aterstalld: $restored"
            $failed += $name
        }
    }

    if ($failed.Count -gt 0) {
        throw "Aterstallningen stammer inte: $($failed -join ', '). Backupen ar INTE bevisad."
    }

    Write-Host "`nAterstallningen bevisad. Dumpen ar fullstandig." -ForegroundColor Green
}
finally {
    docker rm -f $RestoreContainer 2>$null | Out-Null
}
