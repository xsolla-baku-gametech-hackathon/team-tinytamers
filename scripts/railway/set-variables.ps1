# Railway servislerinin environment variable-larini teyin edir.
#
# Teleb olunur: Railway CLI (npm i -g @railway/cli) + `railway login` + `railway link`
# (isledilen qovluq layihenize baglanmis olmalidir).
#
# JWT acari burada avtomatik yaradilir - hec bir yerde fayla yazilmir, yalniz
# Railway-e gonderilir. Bazanin parolu ise Railway-in oz deyiskeni ile referans
# olunur, yeni bu skript onu hec vaxt gormur.
[CmdletBinding()]
param(
    [string]$ApiService = 'aipetsforkids-api',
    [string]$WebService = 'aipetsforkids-web',
    [string]$DatabaseService = 'Postgres',
    [switch]$RotateJwtKey
)

$ErrorActionPreference = 'Stop'

if ($null -eq (Get-Command railway -ErrorAction SilentlyContinue)) {
    throw 'railway CLI tapilmadi. Qurasdirin: npm i -g @railway/cli, sonra: railway login'
}

function Set-Vars([string]$service, [string[]]$pairs) {
    Write-Host "=== $service ===" -ForegroundColor Cyan
    $arguments = @('variables', '--service', $service)
    foreach ($pair in $pairs) {
        $arguments += '--set'
        $arguments += $pair
        Write-Host ("  {0}" -f ($pair -replace '=.*$', '= ...'))
    }
    & railway @arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "$service ucun deyiskenler teyin edile bilmedi." }
}

function New-JwtKey {
    $buffer = New-Object byte[] 48
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    return [Convert]::ToBase64String($buffer)
}

# Railway-de bir servis basqasinin deyiskenine ${{Servis.DEYISKEN}} ile istinad edir.
# Bu setirler oldugu kimi gonderilmelidir - deyeri Railway oz terefinde acir.
$databaseUrl = '${{' + $DatabaseService + '.DATABASE_URL}}'
$webDomain = '${{' + $WebService + '.RAILWAY_PUBLIC_DOMAIN}}'
$apiDomain = '${{' + $ApiService + '.RAILWAY_PUBLIC_DOMAIN}}'

$apiVars = @(
    # Railway hansi Dockerfile-i quracagini bu deyisenden oxuyur. Qoyulmasa
    # Railpack isa dusur ve "could not determine how to build" ile sinir.
    'RAILWAY_DOCKERFILE_PATH=src/PetPal.Api/Dockerfile',
    "DATABASE_URL=$databaseUrl",
    'ASPNETCORE_ENVIRONMENT=Production',
    'PORT=8080',
    'Jwt__Issuer=petpal-api',
    'Jwt__Audience=petpal-app',
    # Railway-de ayrica migrate job yoxdur, ona gore sxem app start-inda tetbiq olunur.
    # EF migrasiyani kilidle apardigi ucun bir nece replica-da da tehlukesizdir.
    'Database__MigrateOnStartup=true',
    "Cors__AllowedOrigins__0=https://$webDomain"
)

$existingJwt = & railway variables --service $ApiService --json 2>$null | ConvertFrom-Json
$hasJwt = $null -ne $existingJwt -and $null -ne $existingJwt.'Jwt__Key'

if ($RotateJwtKey -or -not $hasJwt) {
    $apiVars += "Jwt__Key=$(New-JwtKey)"
    if ($hasJwt) {
        Write-Host 'QEYD: Jwt__Key deyisir - butun aktiv sessiyalar etibarsiz olacaq.' -ForegroundColor Yellow
    }
} else {
    Write-Host 'Jwt__Key artiq var - saxlanilir (deyismek ucun -RotateJwtKey).' -ForegroundColor Yellow
}

Set-Vars $ApiService $apiVars

Set-Vars $WebService @(
    'RAILWAY_DOCKERFILE_PATH=src/PetPal.Web/Dockerfile',
    'PORT=8080',
    "PETPAL_API_BASE_URL=https://$apiDomain/"
)

Write-Host ''
Write-Host 'DEYISKENLER TEYIN OLUNDU.' -ForegroundColor Green
Write-Host 'Railway her deyisiklikden sonra servisi yeniden deploy edir.' -ForegroundColor Yellow
Write-Host 'Yoxlama: scripts\railway\smoke-test.ps1 -WebDomain <...> -ApiDomain <...>'
