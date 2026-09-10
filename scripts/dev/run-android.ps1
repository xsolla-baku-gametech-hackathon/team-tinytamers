<#
.SYNOPSIS
    App-i qoşulu Android telefonda (və ya emulyatorda) işə salır.
.DESCRIPTION
    Fiziki telefonda test etməyin üç şərti var və skript hər üçünü hazırlayır:
      1. Telefon "localhost"-a çata bilmir → development maşınının LAN ünvanı lazımdır.
      2. API yalnız localhost-a bağlıdırsa telefondan görünmür → 0.0.0.0-a bağlanır.
      3. Windows Firewall 5180 portunu bağlayır → qayda yoxdursa xəbərdarlıq verilir.
.PARAMETER HostIp
    Development maşınının LAN ünvanı. Boş buraxılsa Wi-Fi/Ethernet adapteri avtomatik tapılır.
.PARAMETER SkipApi
    API-ni başlatma (artıq işləyirsə).
.EXAMPLE
    .\run-android.ps1
.EXAMPLE
    .\run-android.ps1 -HostIp 192.168.0.103
#>
[CmdletBinding()]
param(
    [string]$HostIp,
    [switch]$SkipApi
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$appProject = Join-Path $repoRoot 'src\PetPal.App\PetPal.App.csproj'
$apiProject = Join-Path $repoRoot 'src\PetPal.Api'
$port = 5180

# ---------- 0. Yol yoxlaması (Android aapt2 ASCII olmayan yolları emal edə bilmir) ----------
if ($repoRoot.Path -match '[^\u0000-\u007F]') {
    Write-Error "Layihə yolunda ASCII olmayan simvol var: $($repoRoot.Path). Android build uğursuz olacaq (APT2265)."
}

# ---------- 1. JDK ----------
if (-not $env:JAVA_HOME -or -not (Test-Path $env:JAVA_HOME)) {
    $jdk = Get-ChildItem 'C:\Program Files\Microsoft' -Directory -Filter 'jdk-*' -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 1
    if (-not $jdk) { Write-Error 'JDK tapılmadı: winget install --id Microsoft.OpenJDK.17' }
    $env:JAVA_HOME = $jdk.FullName
}

# ---------- 2. Cihaz ----------
$adb = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
if (-not (Test-Path $adb)) { Write-Error "adb tapılmadı: $adb" }

$devices = & $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\S' -and $_ -notmatch 'offline' }
if (-not $devices) {
    Write-Error @"
Qoşulu Android cihaz yoxdur.

Fiziki telefon üçün:
  1. Ayarlar → Telefon haqqında → "Build number"a 7 dəfə toxunun
  2. Ayarlar → Developer options → USB debugging = açıq
  3. Telefonu USB ilə qoşun və ekrandakı "Allow USB debugging" sorğusunu təsdiqləyin
  4. Yoxlayın: adb devices
"@
}
Write-Host "Cihaz: $($devices -join ', ')" -ForegroundColor Green

# Emulyatorda LAN ünvanı lazım deyil — 10.0.2.2 host maşına yönləndirir.
$isEmulator = $devices -match '^emulator-'

# ---------- 3. LAN ünvanı ----------
if (-not $isEmulator) {
    if (-not $HostIp) {
        $HostIp = (Get-NetIPAddress -AddressFamily IPv4 |
            Where-Object {
                $_.IPAddress -notlike '127.*' -and
                $_.IPAddress -notlike '169.254.*' -and
                $_.InterfaceAlias -notmatch 'vEthernet|Loopback|WSL'
            } |
            Sort-Object -Property SkipAsSource, InterfaceMetric |
            Select-Object -First 1 -ExpandProperty IPAddress)
    }

    if (-not $HostIp) { Write-Error 'LAN ünvanı tapılmadı. -HostIp ilə əl ilə verin.' }

    $apiUrl = "http://${HostIp}:${port}/"
    Write-Host "API ünvanı (telefon üçün): $apiUrl" -ForegroundColor Cyan
    Write-Host 'Telefon və kompüter EYNİ Wi-Fi şəbəkəsində olmalıdır.' -ForegroundColor DarkGray
}
else {
    $apiUrl = ''   # boş → AppConfig emulyator üçün 10.0.2.2 seçir
    Write-Host 'Emulyator aşkarlandı — 10.0.2.2 istifadə olunacaq.' -ForegroundColor Cyan
}

# ---------- 4. Firewall ----------
$rule = Get-NetFirewallRule -DisplayName 'PetPal dev API' -ErrorAction SilentlyContinue
if (-not $rule -and -not $isEmulator) {
    Write-Warning @"
Windows Firewall $port portunu bloklaya bilər. Administrator PowerShell-də bir dəfə işlədin:

  New-NetFirewallRule -DisplayName 'PetPal dev API' -Direction Inbound ``
    -LocalPort $port -Protocol TCP -Action Allow -Profile Private

(Yalnız Private profil — ictimai şəbəkədə port açılmır.)
"@
}

# ---------- 5. API ----------
if (-not $SkipApi) {
    $healthy = $false
    try { Invoke-WebRequest "http://localhost:$port/health" -TimeoutSec 2 -UseBasicParsing | Out-Null; $healthy = $true } catch { }

    if ($healthy) {
        Write-Warning "API artıq işləyir. Telefondan görünməsi üçün 0.0.0.0-a bağlı olmalıdır. Şübhə varsa dayandırıb bu skripti yenidən işlədin."
    }
    else {
        Write-Host 'API başladılır (bütün interfeyslərdə)…' -ForegroundColor Cyan
        Start-Process -FilePath 'dotnet' `
            -ArgumentList "run --project `"$apiProject`" --no-launch-profile --urls http://0.0.0.0:$port" `
            -WorkingDirectory $repoRoot

        $deadline = (Get-Date).AddSeconds(120)
        do {
            Start-Sleep -Seconds 3
            try { Invoke-WebRequest "http://localhost:$port/health" -TimeoutSec 2 -UseBasicParsing | Out-Null; $healthy = $true } catch { }
        } while (-not $healthy -and (Get-Date) -lt $deadline)

        if (-not $healthy) { Write-Error 'API qalxmadı.' }
        Write-Host 'API hazırdır.' -ForegroundColor Green
    }
}

# ---------- 6. Deploy ----------
Write-Host 'Telefona quraşdırılır və işə salınır…' -ForegroundColor Cyan

$buildArgs = @('build', $appProject, '-f', 'net10.0-android', '-t:Run')
if ($apiUrl) { $buildArgs += "-p:PetPalDevApiUrl=$apiUrl" }

& dotnet @buildArgs
