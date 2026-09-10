<#
.SYNOPSIS
    PostgreSQL-i qaldırır və API-ni development rejimində işə salır.
#>
[CmdletBinding()]
param(
    [switch]$SkipDocker
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

if (-not $SkipDocker) {
    Write-Host 'PostgreSQL qaldırılır…' -ForegroundColor Cyan
    docker compose -f (Join-Path $repoRoot 'docker-compose.yml') up -d postgres

    # Konteyner qalxsa da baza hələ qəbul etməyə bilər; healthcheck-i gözləyirik.
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Seconds 2
        $status = docker inspect -f '{{.State.Health.Status}}' petpal-postgres 2>$null
    } while ($status -ne 'healthy' -and (Get-Date) -lt $deadline)

    if ($status -ne 'healthy') {
        Write-Warning "PostgreSQL hələ hazır deyil (status: $status). API yenə də başladılır."
    }
}

Write-Host 'API başladılır → http://localhost:5180 (sənədlər: /scalar/v1)' -ForegroundColor Green
dotnet run --project (Join-Path $repoRoot 'src\PetPal.Api')
