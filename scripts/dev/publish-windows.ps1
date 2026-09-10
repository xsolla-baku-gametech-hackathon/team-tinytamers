<#
.SYNOPSIS
    AI Pets for Kids-i masaüstündən tək kliklə açılan paketə yığır.
.DESCRIPTION
    Üç layihəni publish edib bir qovluğa yerləşdirir:

        dist\AiPetsForKids\AI Pets for Kids.exe   launcher — baza + API + app-i ardıcıl qaldırır
        dist\AiPetsForKids\api\                   PetPal.Api
        dist\AiPetsForKids\app\                   MAUI Windows app
        dist\AiPetsForKids\docker-compose.yml

    Paket self-contained-dir: hədəf maşında .NET runtime lazım deyil, qovluğu
    olduğu kimi başqa Windows kompüterinə köçürmək olar. Əvəzində paket
    ~500 MB olur və ilk build bir neçə dəqiqə çəkir.

    PostgreSQL istisnadır — o, Docker konteynerində qalır və launcher lazım
    olanda Docker Desktop-ı özü başladır.
.PARAMETER NoShortcut
    Masaüstünə "AI Pets for Kids" qısayolu yaratmır.
.PARAMETER OutputDirectory
    Paketin yeri. Standart: <repo>\dist\AiPetsForKids
.EXAMPLE
    .\scripts\dev\publish-windows.ps1
.EXAMPLE
    .\scripts\dev\publish-windows.ps1 -NoShortcut -OutputDirectory "D:\AI Pets for Kids"
#>
[CmdletBinding()]
param(
    [switch]$NoShortcut,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'dist\AiPetsForKids' }

$windowsTfm = 'net10.0-windows10.0.19041.0'

function Invoke-Publish {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Destination,
        [string[]]$ExtraArgs = @()
    )

    $arguments = @('publish', (Join-Path $repoRoot $Project), '-c', 'Release', '-o', $Destination) + $ExtraArgs

    Write-Host "  dotnet $($arguments -join ' ')" -ForegroundColor DarkGray
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Publish uğursuz oldu: $Project" }
}

# Köhnə fayllar qarışmasın deyə paket qovluğu sıfırdan yığılır. Yalnız öz
# çıxış qovluğumuza toxunuruq — səhv yol verilibsə heç nə silinmir.
if (Test-Path $OutputDirectory) {
    $resolved = (Resolve-Path $OutputDirectory).Path
    if ($resolved -eq $repoRoot -or -not $resolved.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Təhlükəsizlik yoxlaması: '$resolved' repo daxilində deyil, silinmir. Başqa -OutputDirectory seçin."
    }
    Write-Host "Köhnə paket silinir: $resolved" -ForegroundColor DarkGray
    Remove-Item $resolved -Recurse -Force
}

Write-Host "AI Pets for Kids masaüstü paketi yığılır → $OutputDirectory" -ForegroundColor Cyan

Write-Host "`n[1/3] API…" -ForegroundColor Cyan
Invoke-Publish -Project 'src\PetPal.Api' -Destination (Join-Path $OutputDirectory 'api') -ExtraArgs @(
    '-r', 'win-x64', '--self-contained', 'true'
)

Write-Host "`n[2/3] App (MAUI Windows)…" -ForegroundColor Cyan
# RID və self-contained ayarları qəsdən -p:PetPalDesktopPackage ilə ötürülür —
# səbəbi PetPal.App.csproj-dakı şərh (çoxhədəfli restore + Mono runtime paketi).
Invoke-Publish -Project 'src\PetPal.App' -Destination (Join-Path $OutputDirectory 'app') -ExtraArgs @(
    '-f', $windowsTfm, '-p:PetPalDesktopPackage=true'
)

Write-Host "`n[3/3] Launcher…" -ForegroundColor Cyan
Invoke-Publish -Project 'src\PetPal.Launcher' -Destination $OutputDirectory -ExtraArgs @(
    '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishSingleFile=true', '-p:EnableCompressionInSingleFile=true'
)

Copy-Item (Join-Path $repoRoot 'docker-compose.yml') -Destination $OutputDirectory -Force

# Kök qovluqda yalnız launcher exe-si görünsün — qalan publish artefactları lazım deyil.
Get-ChildItem $OutputDirectory -Filter '*.pdb' -File | Remove-Item -Force

$launcher = Join-Path $OutputDirectory 'AI Pets for Kids.exe'
if (-not (Test-Path $launcher)) { throw "Launcher yaranmadı: $launcher" }

if (-not $NoShortcut) {
    $shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'AI Pets for Kids.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    $link.TargetPath = $launcher
    $link.WorkingDirectory = $OutputDirectory
    $link.IconLocation = "$launcher,0"
    $link.Description = 'AI Pets for Kids — uşaqlar üçün ağıllı AI pet'
    $link.Save()
    Write-Host "`nMasaüstü qısayolu: $shortcut" -ForegroundColor Green
}

$sizeMb = [math]::Round(((Get-ChildItem $OutputDirectory -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 0)
Write-Host "`nHazırdır — $sizeMb MB" -ForegroundColor Green
Write-Host "Açmaq üçün: $launcher" -ForegroundColor Green
