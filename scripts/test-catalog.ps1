param([int]$Port = 5082, [ValidateSet('catalog', 'reservations')][string]$Suite = 'catalog')
$ErrorActionPreference = 'Stop'
$rccRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $rccRoot
New-Item -ItemType Directory -Path '.local' -Force | Out-Null
$rccDotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $rccDotnet) { $rccDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet/dotnet.exe' }
$rccConfig = @{}
Get-Content .env | ForEach-Object { if ($_ -match '^([^#=]+)=(.*)$') { $rccConfig[$Matches[1]] = $Matches[2] } }
if (-not $rccConfig.POSTGRES_USER -or -not $rccConfig.POSTGRES_PASSWORD) { throw 'Configure PostgreSQL local mediante .env. Se requiere CREATEDB.' }
$rccDatabase = 'rcc_e2e_' + [Guid]::NewGuid().ToString('N')
$env:ConnectionStrings__Rcc = "Host=127.0.0.1;Port=5432;Database=$rccDatabase;Username=$($rccConfig.POSTGRES_USER);Password=$($rccConfig.POSTGRES_PASSWORD)"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:RCC_E2E_ADMIN_EMAIL = "e2e-$([Guid]::NewGuid().ToString('N'))@example.test"
$env:RCC_E2E_ADMIN_PASSWORD = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) + 'Aa1!'
$env:Administrator__Email = $env:RCC_E2E_ADMIN_EMAIL
$env:Administrator__Password = $env:RCC_E2E_ADMIN_PASSWORD
$env:RCC_CATALOG_BASE_URL = "http://127.0.0.1:$Port"
$env:Logging__LogLevel__Default = 'Warning'
$rccServer = $null
$rccResult = 1
try {
    $rccProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
    try { $rccProbe.Start() } finally { $rccProbe.Stop() }
    & $rccDotnet src/Rcc.Api/bin/Release/net10.0/Rcc.Api.dll --initialize-identity
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo inicializar la base aislada.' }
    Remove-Item Env:Administrator__Password, Env:Administrator__Email
    $rccContentRoot = Join-Path $rccRoot 'src/Rcc.Api'
    $rccServer = Start-Process -FilePath $rccDotnet -ArgumentList @('src/Rcc.Api/bin/Release/net10.0/Rcc.Api.dll', '--contentRoot', ('"' + $rccContentRoot + '"'), '--urls', $env:RCC_CATALOG_BASE_URL) -WorkingDirectory $rccRoot -WindowStyle Hidden -RedirectStandardOutput '.local/catalog-e2e-server.log' -RedirectStandardError '.local/catalog-e2e-server-error.log' -PassThru
    $rccReady = $false
    for ($rccAttempt = 0; $rccAttempt -lt 40; $rccAttempt++) {
        if ($rccServer.HasExited) { break }
        try { if ((Invoke-WebRequest "$env:RCC_CATALOG_BASE_URL/api/health/ready" -TimeoutSec 2 -NoProxy).StatusCode -eq 200) { $rccReady = $true; break } } catch { }
        Start-Sleep -Milliseconds 500
    }
    if (-not $rccReady) { throw 'La API de pruebas no inició; revisar .local/catalog-e2e-server-error.log.' }
    & npm run "test:$Suite" --prefix frontend
    $rccResult = $LASTEXITCODE
} finally {
    if ($rccServer -and -not $rccServer.HasExited) { Stop-Process -Id $rccServer.Id }
    # Only the random database named above is eligible for this cleanup.
    if ($rccDatabase -notmatch '^rcc_e2e_[0-9a-f]{32}$' -or $env:ConnectionStrings__Rcc -notlike "*Database=$rccDatabase;*") { throw 'Unsafe test database cleanup.' }
    & $rccDotnet ef database drop --force --project src/Rcc.Api --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo limpiar la base de pruebas.' }
}
exit $rccResult
