param([switch]$DesdePortapapeles)
$ErrorActionPreference = 'Stop'
$rccSecure = $null
if ($DesdePortapapeles) {
    Read-Host 'Copia AHORA la URI privada de Neon, vuelve y pulsa Enter sin pegarla' | Out-Null
    $rccRaw = ([string](Get-Clipboard -Raw)).Trim()
} else {
    $rccSecure = Read-Host 'Pega la URI privada PostgreSQL de Neon y pulsa Enter (no se muestra)' -AsSecureString
    $rccRaw = [System.Net.NetworkCredential]::new('', $rccSecure).Password.Trim()
}
try {
    $rccUri = $null
    if (-not [Uri]::TryCreate($rccRaw, [UriKind]::Absolute, [ref]$rccUri) -or $rccUri.Scheme -notin @('postgres','postgresql') -or -not $rccUri.Host.EndsWith('.neon.tech')) { throw 'Se requiere una URI de conexion PostgreSQL de Neon. No compartas su valor.' }
    $rccUserInfo = $rccUri.UserInfo.Split(':', 2)
    if ($rccUserInfo.Length -ne 2) { throw 'La URI debe incluir usuario y password de la conexion.' }
    $rccConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
    $rccConnection.Add('Host', $rccUri.Host)
    $rccConnection.Add('Port', $(if ($rccUri.Port -gt 0) { $rccUri.Port } else { 5432 }))
    $rccConnection.Add('Database', [Uri]::UnescapeDataString($rccUri.AbsolutePath.TrimStart('/')))
    $rccConnection.Add('Username', [Uri]::UnescapeDataString($rccUserInfo[0]))
    $rccConnection.Add('Password', [Uri]::UnescapeDataString($rccUserInfo[1]))
    $rccConnection.Add('SSL Mode', 'VerifyFull')
    $rccConnection.Add('Maximum Pool Size', '10')
    if ($rccConnection.ConnectionString -match '[\r\n]') { throw 'La conexion no debe contener saltos de linea.' }
    $rccFolder = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'RCC\deployment'
    New-Item -ItemType Directory -Path $rccFolder -Force | Out-Null
    $rccPath = Join-Path $rccFolder 'neon-connection.txt'
    if (Test-Path -LiteralPath $rccPath) { throw 'Ya existe una conexion privada preparada. Conservala o renombrala antes de volver a preparar otra.' }
    [IO.File]::WriteAllText($rccPath, $rccConnection.ConnectionString)
    Write-Host "Conexion preparada en el perfil privado: $rccPath"
    Write-Host 'Pega su contenido SOLO en ConnectionStrings__Rcc del backend Render. No en chat, Git ni frontend.'
} finally {
    $rccRaw = $null
    $rccUserInfo = $null
    $rccConnection = $null
    if ($null -ne $rccSecure) { $rccSecure.Dispose() }
}
