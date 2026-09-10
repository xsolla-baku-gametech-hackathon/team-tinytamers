<#
.SYNOPSIS
    Development bazasını sıfırdan qurur (bütün məlumat silinir).
.DESCRIPTION
    Yalnız lokal development üçündür. Konteyneri və volume-u silir,
    yenidən qaldırır və migrasiya + seed işlədir.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$compose = Join-Path $repoRoot 'docker-compose.yml'

if (-not $PSCmdlet.ShouldProcess('petpal development bazası', 'BÜTÜN məlumatı sil və yenidən qur')) {
    return
}

Write-Host 'Konteyner və volume silinir…' -ForegroundColor Yellow
docker compose -f $compose down -v

Write-Host 'PostgreSQL yenidən qaldırılır…' -ForegroundColor Cyan
docker compose -f $compose up -d postgres

$deadline = (Get-Date).AddSeconds(60)
do {
    Start-Sleep -Seconds 2
    $status = docker inspect -f '{{.State.Health.Status}}' petpal-postgres 2>$null
} while ($status -ne 'healthy' -and (Get-Date) -lt $deadline)

if ($status -ne 'healthy') {
    Write-Error "PostgreSQL hazır olmadı (status: $status)."
}

Write-Host 'Migrasiya və seed işlədilir…' -ForegroundColor Cyan
dotnet run --project (Join-Path $repoRoot 'src\PetPal.Api') -- --migrate-only

Write-Host 'Hazırdır.' -ForegroundColor Green
