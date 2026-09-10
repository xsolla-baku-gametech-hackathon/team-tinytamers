<#
.SYNOPSIS
    Android APK-sını qurur.
.DESCRIPTION
    JAVA_HOME-u avtomatik tapır və Android build-in ən çox rast gəlinən
    problemini — yolda ASCII olmayan simvol — əvvəlcədən yoxlayır.
.PARAMETER InstallDependencies
    Android SDK komponentlərini qurur (ilk dəfə üçün).
.PARAMETER Configuration
    Debug (default) və ya Release.
#>
[CmdletBinding()]
param(
    [switch]$InstallDependencies,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repoRoot 'src\PetPal.App\PetPal.App.csproj'

# aapt2 yolda ASCII olmayan simvolları emal edə bilmir (APT2265).
if ($repoRoot.Path -match '[^\u0000-\u007F]') {
    Write-Error @"
Layihə yolunda ASCII olmayan simvol var:
  $($repoRoot.Path)

Android-in aapt2 aləti belə yolları emal edə bilmir (xəta APT2265) və build
uğursuz olacaq. Repozitoriyanı yalnız ingilis hərfləri olan yola köçürün, məsələn:
  C:\Users\$env:USERNAME\Desktop\petpal

Digər hədəflər (Windows, API, testlər) cari yolda problemsiz işləyir.
"@
}

if (-not $env:JAVA_HOME -or -not (Test-Path $env:JAVA_HOME)) {
    $jdk = Get-ChildItem 'C:\Program Files\Microsoft' -Directory -Filter 'jdk-*' -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1

    if (-not $jdk) {
        Write-Error 'JDK tapılmadı. Quraşdırın: winget install --id Microsoft.OpenJDK.17'
    }

    $env:JAVA_HOME = $jdk.FullName
    Write-Host "JAVA_HOME təyin olundu: $env:JAVA_HOME" -ForegroundColor DarkGray
}

if ($InstallDependencies) {
    Write-Host 'Android SDK komponentləri qurulur…' -ForegroundColor Cyan
    dotnet build $project -f net10.0-android -t:InstallAndroidDependencies -p:AcceptAndroidSDKLicenses=true
}

Write-Host "Android build ($Configuration)…" -ForegroundColor Cyan
dotnet build $project -f net10.0-android -c $Configuration
