param([ValidateSet('qwen2.5:1.5b')][string]$Modelo = 'qwen2.5:1.5b')
$ErrorActionPreference = 'Stop'
$rccOllama = Get-Command ollama -ErrorAction SilentlyContinue
$rccOllamaPath = if ($rccOllama) { $rccOllama.Source } else { Join-Path $env:LOCALAPPDATA 'Programs\Ollama\ollama.exe' }
if (-not (Test-Path -LiteralPath $rccOllamaPath)) { throw 'Instala Ollama desde https://ollama.com/download/windows y vuelve a ejecutar este script.' }
try { Invoke-RestMethod http://127.0.0.1:11434/api/version -TimeoutSec 3 | Out-Null }
catch { throw 'Abre Ollama desde Inicio y vuelve a ejecutar este script.' }
& $rccOllamaPath pull $Modelo
if ($LASTEXITCODE -ne 0) { throw 'No se pudo descargar el modelo.' }
foreach ($rccSetting in @(@('OpenAI:Provider','Ollama'), @('OpenAI:Model',$Modelo), @('OpenAI:MaxOutputTokens','1024'), @('OpenAI:DailyRequestLimit','20'), @('OpenAI:Enabled','true'))) {
    & dotnet user-secrets set $rccSetting[0] $rccSetting[1] --id rcc-api-local-development | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo guardar la configuracion.' }
}
Write-Host 'IA local configurada sin clave. Reinicia RCC. Ollama debe seguir abierto; no hay llamadas a OpenAI con este proveedor.'
