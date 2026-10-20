param([int]$Port = 5084)
& "$PSScriptRoot/test-catalog.ps1" -Port $Port -Suite payments
exit $LASTEXITCODE
