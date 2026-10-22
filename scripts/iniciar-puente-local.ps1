# Starts only the local authenticated relay. It does not publish a tunnel or copy credentials.
$ErrorActionPreference = 'Stop'
$rccRoot = Split-Path $PSScriptRoot -Parent
$rccStore = 'rcc-ai-relay-local'
$rccDotnet = (Get-Command dotnet -ErrorAction Stop).Source
try { Invoke-RestMethod http://127.0.0.1:11434/api/version -TimeoutSec 3 | Out-Null } catch { throw 'Abre Ollama primero.' }
& $rccDotnet build (Join-Path $rccRoot 'src\Rcc.AiRelay\Rcc.AiRelay.csproj') --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el puente.' }
$rccBytes = New-Object byte[] 32
$rccGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rccGenerator.GetBytes($rccBytes)
$rccGenerator.Dispose()
$rccSecret = -join ($rccBytes | ForEach-Object { $_.ToString('x2') })
$rccExpiry = [DateTimeOffset]::UtcNow.AddHours(8).AddMinutes(-1).ToString('O')
$rccSaved = @{ 'Relay:Token' = $rccSecret; 'Relay:ExpiresUtc' = $rccExpiry } | ConvertTo-Json | & $rccDotnet user-secrets set --id $rccStore 2>&1
if ($LASTEXITCODE -ne 0) { throw 'No se pudo guardar la credencial en user-secrets.' }
$rccOldToken = [Environment]::GetEnvironmentVariable('Relay__Token', 'Process')
$rccOldExpiry = [Environment]::GetEnvironmentVariable('Relay__ExpiresUtc', 'Process')
try {
    $env:Relay__Token = $rccSecret
    $env:Relay__ExpiresUtc = $rccExpiry
    Write-Host 'Puente local en 127.0.0.1:5092, con credencial nueva y vencimiento de 8 horas.'
    Write-Host 'La credencial esta en el almacen privado rcc-ai-relay-local; no se imprime ni se copia. Ctrl+C cierra el puente.'
    & $rccDotnet (Join-Path $rccRoot 'src\Rcc.AiRelay\bin\Release\net10.0\Rcc.AiRelay.dll')
} finally {
    [Environment]::SetEnvironmentVariable('Relay__Token', $rccOldToken, 'Process')
    [Environment]::SetEnvironmentVariable('Relay__ExpiresUtc', $rccOldExpiry, 'Process')
    $rccSecret = $null
    $rccSaved = $null
    $rccBytes = $null
}
