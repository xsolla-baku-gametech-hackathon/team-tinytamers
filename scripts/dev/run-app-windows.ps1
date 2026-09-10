<#
.SYNOPSIS
    App-i Windows hədəfində işə salır — dizayn və axın yoxlaması üçün ən sürətli yol.
.DESCRIPTION
    API-nin ayrıca işlədiyini gözləyir (scripts\dev\start-api.ps1).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

try {
    $health = Invoke-WebRequest -Uri 'http://localhost:5180/health' -TimeoutSec 3 -UseBasicParsing
    if ($health.StatusCode -ne 200) { throw }
}
catch {
    Write-Warning 'API http://localhost:5180 ünvanında cavab vermir. Əvvəlcə scripts\dev\start-api.ps1 işlədin.'
}

dotnet run --project (Join-Path $repoRoot 'src\PetPal.App') -f net10.0-windows10.0.19041.0
