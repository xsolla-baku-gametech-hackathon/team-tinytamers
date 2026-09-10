# Railway deploy-undan sonra en vacib davranislari yoxlayir. Hec ne yazmir, yalniz oxuyur.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WebDomain,
    [Parameter(Mandatory = $true)][string]$ApiDomain
)

$ErrorActionPreference = 'Stop'
$failures = @()

function Test-Endpoint([string]$name, [string]$url, [int[]]$expected) {
    try {
        $response = Invoke-WebRequest -Uri $url -Method GET -UseBasicParsing -TimeoutSec 90 -MaximumRedirection 0 -ErrorAction Stop
        $code = [int]$response.StatusCode
    } catch {
        if ($null -ne $_.Exception.Response) {
            $code = [int]$_.Exception.Response.StatusCode
        } else {
            $script:failures += "$name : cavab yoxdur ($($_.Exception.Message))"
            Write-Host ("  [FAIL] {0} - cavab yoxdur" -f $name) -ForegroundColor Red
            return
        }
    }

    if ($expected -contains $code) {
        Write-Host ("  [OK]   {0} -> {1}" -f $name, $code) -ForegroundColor Green
    } else {
        $script:failures += "$name : gozlenilen $($expected -join '/'), alinan $code"
        Write-Host ("  [FAIL] {0} -> {1} (gozlenilen: {2})" -f $name, $code, ($expected -join '/')) -ForegroundColor Red
    }
}

Write-Host '=== API ===' -ForegroundColor Cyan
Test-Endpoint 'API /health/live'  "https://$ApiDomain/health/live"  @(200)
Test-Endpoint 'API /health/ready (baza qosulur)' "https://$ApiDomain/health/ready" @(200)
# Scalar/OpenAPI yalniz Development-de acilir - produksiyada 404 gozlenilir.
Test-Endpoint 'API /scalar/v1 (prod-da bagli olmali)' "https://$ApiDomain/scalar/v1" @(404)
Test-Endpoint 'API /openapi/v1.json (prod-da bagli olmali)' "https://$ApiDomain/openapi/v1.json" @(404)
# Tokensiz oyun endpoint-i 401 qaytarmalidir (autentifikasiya isleyir).
Test-Endpoint 'API /api/home (tokensiz 401)' "https://$ApiDomain/api/home" @(401)

Write-Host '=== Web ===' -ForegroundColor Cyan
Test-Endpoint 'Web /health/live' "https://$WebDomain/health/live" @(200)
Test-Endpoint 'Web ana sehife' "https://$WebDomain/" @(200)
Test-Endpoint 'Web appsettings.json (API unvani)' "https://$WebDomain/appsettings.json" @(200)
Test-Endpoint 'Web blazor yukleyicisi' "https://$WebDomain/_framework/blazor.webassembly.js" @(200)
Test-Endpoint 'Web dizayn faylı' "https://$WebDomain/_content/PetPal.App.Ui/css/app.css" @(200)
# SPA marsrutu birbasa acilanda index.html qaytarilmalidir (404 yox).
Test-Endpoint 'Web SPA marsrutu /care' "https://$WebDomain/care" @(200)

Write-Host '=== Cache basliqlari ===' -ForegroundColor Cyan
# Adinda hash olmayan fayl uzun cache alsa, deploy movcud cihazlara CATMIR:
# brauzer kohne yukleyicini (sayt acilmir) ve ya kohne CSS-i (dizayn dagilir)
# qaytarir. Bu, yalniz sayti evvel acmis cihazda gorunur - ona gore burada
# yoxlanilir. Hash dasiyan .wasm fayllari bu qaydadan kenardir.
function Test-Revalidated([string]$name, [string]$path) {
    try {
        $response = Invoke-WebRequest -Uri "https://$WebDomain/$path" -Method GET -UseBasicParsing -TimeoutSec 90 -ErrorAction Stop
        $cache = "$($response.Headers['Cache-Control'])"
    } catch {
        $script:failures += "$name : cavab yoxdur ($($_.Exception.Message))"
        Write-Host ("  [FAIL] {0} - cavab yoxdur" -f $name) -ForegroundColor Red
        return
    }

    if ($cache -match 'immutable' -or $cache -match 'max-age=[1-9]') {
        $script:failures += "$name : uzun cache ('$cache') - adinda hash yoxdur, her deploy-da yenilenmelidir"
        Write-Host ("  [FAIL] {0} -> {1}" -f $name, $cache) -ForegroundColor Red
    } else {
        Write-Host ("  [OK]   {0} -> {1}" -f $name, $(if ($cache) { $cache } else { '(cache yoxdur)' })) -ForegroundColor Green
    }
}

Test-Revalidated 'blazor.webassembly.js revalidasiya olunur' '_framework/blazor.webassembly.js'
Test-Revalidated 'dotnet.js revalidasiya olunur' '_framework/dotnet.js'
Test-Revalidated 'app.css revalidasiya olunur' '_content/PetPal.App.Ui/css/app.css'
Test-Revalidated 'theme.css revalidasiya olunur' '_content/PetPal.App.Ui/css/theme.css'
Test-Revalidated 'pet.css revalidasiya olunur' '_content/PetPal.App.Ui/css/pet.css'
Test-Revalidated 'petCare.js revalidasiya olunur' '_content/PetPal.App.Ui/js/petCare.js'

Write-Host '=== Web -> API baglantisi ===' -ForegroundColor Cyan
try {
    # Brauzer sixilmis cavab isteyir - kohne .gz nusxesi verilirse bu tutur.
    $settings = Invoke-RestMethod -Uri "https://$WebDomain/appsettings.json" -TimeoutSec 60 -Headers @{ "Accept-Encoding" = "gzip" }
    $configured = "$($settings.ApiBaseUrl)".TrimEnd('/')
    $expected = "https://$ApiDomain"
    if ($configured -eq $expected) {
        Write-Host ("  [OK]   ApiBaseUrl = {0}" -f $configured) -ForegroundColor Green
    } else {
        $failures += "ApiBaseUrl : gozlenilen $expected, alinan $configured"
        Write-Host ("  [FAIL] ApiBaseUrl = {0} (gozlenilen: {1})" -f $configured, $expected) -ForegroundColor Red
    }
} catch {
    $failures += "ApiBaseUrl oxuna bilmedi: $($_.Exception.Message)"
    Write-Host '  [FAIL] appsettings.json oxuna bilmedi' -ForegroundColor Red
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "SMOKE TEST UGURSUZ ($($failures.Count) problem):" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
Write-Host 'SMOKE TEST UGURLA KECDI.' -ForegroundColor Green
Write-Host ''
Write-Host 'Elave elle yoxlama: brauzerde ' -NoNewline
Write-Host "https://$WebDomain" -ForegroundColor Cyan -NoNewline
Write-Host ' acib qeydiyyatdan kecin, usaq profili yaradin, yumurtani acin.'
