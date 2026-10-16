param(
    [string]$PostgresBin = (Join-Path $env:LOCALAPPDATA 'RCC/tools/postgresql17/pgsql/bin'),
    [int]$Port = 55432,
    [switch]$Stop
)
$ErrorActionPreference = 'Stop'
$rccRoot = Split-Path $PSScriptRoot -Parent
$rccLocal = Join-Path $rccRoot '.local'
$rccData = Join-Path $rccLocal 'postgres-data'
$rccConfig = Join-Path $rccLocal 'postgres.json'
$rccControl = Join-Path $PostgresBin 'pg_ctl.exe'
if (-not (Test-Path $rccControl)) { throw "No se encontró PostgreSQL en $PostgresBin" }
if ($Stop) {
    & $rccControl -D $rccData stop -m fast
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo detener PostgreSQL.' }
    exit 0
}
New-Item -ItemType Directory -Force $rccLocal | Out-Null
if (-not (Test-Path (Join-Path $rccData 'PG_VERSION'))) {
    if (Test-Path $rccConfig) { throw 'Existe configuración sin cluster. Revisar .local manualmente.' }
    $rccPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    $rccPasswordFile = Join-Path $rccLocal 'postgres-init.password'
    Set-Content -LiteralPath $rccPasswordFile -Value $rccPassword -NoNewline
    try {
        & (Join-Path $PostgresBin 'initdb.exe') -D $rccData -U rcc --encoding=UTF8 --locale=C --auth=scram-sha-256 --pwfile=$rccPasswordFile
        if ($LASTEXITCODE -ne 0) { throw 'initdb falló.' }
        @{ Port = $Port; Password = $rccPassword } | ConvertTo-Json | Set-Content -LiteralPath $rccConfig
    } finally {
        Remove-Item -LiteralPath $rccPasswordFile -ErrorAction SilentlyContinue
    }
}
if (-not (Test-Path $rccConfig)) { throw 'Falta .local/postgres.json; no se modificó el cluster existente.' }
$rccSettings = Get-Content -LiteralPath $rccConfig -Raw | ConvertFrom-Json
$Port = $rccSettings.Port
& $rccControl -D $rccData status *> $null
if ($LASTEXITCODE -ne 0) {
    & $rccControl -D $rccData -l (Join-Path $rccLocal 'postgres.log') -o "-h 127.0.0.1 -p $Port" -w start
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL no pudo iniciar; revisar .local/postgres.log.' }
}
$env:PGPASSWORD = $rccSettings.Password
try {
    $rccExists = & (Join-Path $PostgresBin 'psql.exe') -h 127.0.0.1 -p $Port -U rcc -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = 'rcc'"
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar PostgreSQL.' }
    if ($rccExists -ne '1') {
        & (Join-Path $PostgresBin 'createdb.exe') -h 127.0.0.1 -p $Port -U rcc rcc
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la base rcc.' }
    }
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
$rccConnection = "Host=127.0.0.1;Port=$Port;Database=rcc;Username=rcc;Password=$($rccSettings.Password)"
& dotnet user-secrets set 'ConnectionStrings:Rcc' $rccConnection --project (Join-Path $rccRoot 'src/Rcc.Api')
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron guardar los user-secrets.' }
Write-Output "PostgreSQL local listo en 127.0.0.1:$Port; conexión guardada en user-secrets."
