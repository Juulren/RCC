param([int]$Port = 5083)
& "$PSScriptRoot/test-catalog.ps1" -Port $Port -Suite reservations
exit $LASTEXITCODE
