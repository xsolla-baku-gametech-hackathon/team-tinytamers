#!/usr/bin/env bash
#
# Bootcamp replay — yol manifestini yaradır və ƏHATƏNİ YOXLAYIR.
#
# Manifest `docs/bootcamp/manifest.tsv` faylıdır, üç sütunlu:
#
#   addım <TAB> növ <TAB> hədəf
#
#   file   — faylı olduğu kimi köçür
#   css    — app.css-in adı verilən bölməsini əlavə et
#   write  — bu addımda ƏL İLƏ yazılan fayl (Program.cs, körpü faylları)
#
# Ən vacib hissə sonda: repodakı hər izlənən fayl DƏQİQ BİR addıma düşməlidir.
# Heç bir addıma düşməyən fayl bootcamp-də səssizcə heç vaxt gəlmir — bunu
# yalnız bu yoxlama tutur.
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="$ROOT/docs/bootcamp/manifest.tsv"
TMP="$(mktemp)"
trap 'rm -f "$TMP" "$TMP".*' EXIT

cd "$ROOT"
mkdir -p "$(dirname "$OUT")"
: > "$TMP"
: > "$TMP.seen"

# Verilən şablonlara uyğun İZLƏNƏN faylları addıma yazır.
#
# BİRİNCİ TƏYİNAT QAZANIR: artıq bir addıma yazılmış fayl ikinci dəfə
# yazılmır. Bu, sonda gələn geniş şablonların (`docs/*`, `scripts/dev/*`)
# əvvəldə dəqiq təyin olunmuş faylları təkrar götürməsinin qarşısını alır —
# əks halda eyni fayl iki addımda görünərdi.
f() {
    local step="$1"; shift
    local pattern path
    for pattern in "$@"; do
        while read -r path; do
            [ -n "$path" ] || continue
            grep -qxF "$path" "$TMP.seen" 2>/dev/null && continue
            printf '%s\n' "$path" >> "$TMP.seen"
            printf '%s\tfile\t%s\n' "$step" "$path" >> "$TMP"
        done < <(git ls-files "$pattern")
    done
}

# Faylı təkrar, ƏSLİ ilə köçürür — yazılan faylın son addımı üçün.
# «Yazılan hər fayl son addımında əsli ilə əvəz olunur» qaydası budur.
fx() {
    local step="$1"; shift
    local path
    for path in "$@"; do
        printf '%s\tfile\t%s\n' "$step" "$path" >> "$TMP"
        grep -qxF "$path" "$TMP.seen" 2>/dev/null || printf '%s\n' "$path" >> "$TMP.seen"
    done
}

# app.css bölməsi (şərhdəki adı ilə).
c() {
    local step="$1"; shift
    local name
    for name in "$@"; do
        printf '%s\tcss\t%s\n' "$step" "$name" >> "$TMP"
    done
}

# Bu addımda əl ilə yazılan fayl.
#
# Yazılan fayl «götürülmüş» sayılır: həmin addımdakı geniş şablon onu ƏSLİ ilə
# köçürməməlidir. Körpü faylları məhz buna görə azaldılmış variantla gəlir —
# `w` sətri qovluq şablonundan ƏVVƏL yazılmalıdır.
w() {
    local step="$1"; shift
    local path
    for path in "$@"; do
        printf '%s\twrite\t%s\n' "$step" "$path" >> "$TMP"
        grep -qxF "$path" "$TMP.seen" 2>/dev/null || printf '%s\n' "$path" >> "$TMP.seen"
    done
}

# ============================ Faza 0 — Bünövrə ============================

f 1 .gitignore .dockerignore PetPal.slnx "*.csproj" .github/workflows/ci.yml .claude/settings.json
f 2 "src/PetPal.Shared/Enums/*" "src/PetPal.Shared/Validation/*" \
    src/PetPal.Shared/Dtos/ApiErrorResponse.cs "src/PetPal.Api/Common/*"
f 3 "src/PetPal.Api/Entities/*" src/PetPal.Api/Data/AppDbContext.cs \
    src/PetPal.Api/Data/StringListConverter.cs \
    src/PetPal.Api/Hosting/DatabaseConnectionString.cs \
    src/PetPal.Api/Hosting/HostRunModeParser.cs \
    "src/PetPal.Api/Migrations/20260804124207_InitialCreate*" \
    "src/PetPal.Api/appsettings*.json" src/PetPal.Api/PetPal.Api.http \
    "src/PetPal.Api/Properties/*"
w 3 src/PetPal.Api/Program.cs src/PetPal.Api/Hosting/DatabaseBootstrapper.cs
f 4 docker-compose.yml src/PetPal.Api/Dockerfile scripts/dev/reset-db.ps1 .env.example

# ============================ Faza 1 — API özəyi ============================

# Körpü faylları: `w` sətri şablondan ƏVVƏLdir ki, qovluq şablonu onları
# əsli ilə köçürməsin — bu addımda azaldılmış variant yazılır.
w 5 src/PetPal.Api/Rewards/RewardService.cs
f 5 "src/PetPal.Api/Rewards/*" "src/PetPal.Shared/Dtos/Rewards/*"
f 6 "src/PetPal.Api/Missions/*" "src/PetPal.Shared/Dtos/Missions/*"
w 7 src/PetPal.Api/Pets/PetEndpoints.cs
f 7 src/PetPal.Api/Pets/IPetService.cs src/PetPal.Api/Pets/PetService.cs \
    src/PetPal.Api/Pets/PetFoods.cs src/PetPal.Api/Pets/PetAccessories.cs \
    src/PetPal.Api/Pets/PetProgression.cs src/PetPal.Shared/Dtos/Pets/PetDtos.cs
# AdaptiveEngine burada gəlir, Learning ilə yox: o, 55 sətirlik və HEÇ BİR
# using-i olmayan saf qayda faylıdır, ProgressService isə ondan asılıdır.
# Bu bir köçürmə «Progress → Learning» körpüsünü tamamilə aradan qaldırır.
f 8 src/PetPal.Api/Learning/AdaptiveEngine.cs \
    "src/PetPal.Api/Progress/*" "src/PetPal.Shared/Dtos/Progress/*"
# Arena qəsdən burada YOXDUR — o, 53-cü addımın işidir.
w 9 src/PetPal.Api/Learning/LearningService.cs
f 9 src/PetPal.Api/Learning/ILearningService.cs \
    src/PetPal.Api/Learning/IQuestionSelector.cs \
    src/PetPal.Api/Learning/LearningEndpoints.cs \
    src/PetPal.Api/Learning/LearningService.cs \
    src/PetPal.Api/Learning/QuestionSelector.cs \
    src/PetPal.Shared/Dtos/Learning/LearningDtos.cs
f 10 "src/PetPal.Api/Data/Questions/*" src/PetPal.Api/Data/DbInitializer.cs
# Seed artıq mövcuddur — bootstrapper öz əslinə qayıdır.
fx 10 src/PetPal.Api/Hosting/DatabaseBootstrapper.cs
f 11 "src/PetPal.Api/Games/*" "src/PetPal.Shared/Dtos/Games/*"
f 12 "src/PetPal.Api/Auth/*" "src/PetPal.Api/Security/*" "src/PetPal.Shared/Dtos/Auth/*" \
     tests/PetPal.Tests/ApiTestClient.cs tests/PetPal.Tests/TestWebAppFactory.cs \
     tests/PetPal.Tests/AuthTests.cs tests/PetPal.Tests/DatabaseConnectionStringTests.cs
# HomeService ev ekranının vəziyyətini bütün qatlardan yığır — Ai də daxil.
# Ona görə burada azaldılmış variantla gəlir, 44-cü addımda əslinə qayıdır.
w 13 src/PetPal.Api/Home/HomeService.cs
f 13 "src/PetPal.Api/Home/*" "src/PetPal.Shared/Dtos/Home/*"

# ============================ Faza 2 — App qabığı ============================

f 14 "src/PetPal.App.Ui/Services/*" src/PetPal.App.Ui/_Imports.razor \
     src/PetPal.App.Ui/Routes.razor tests/PetPal.App.Tests/AppStateTests.cs \
     tests/PetPal.App.Tests/RazorBindingTests.cs
f 15 src/PetPal.App.Ui/wwwroot/css/theme.css "src/PetPal.App.Ui/Components/Layout/*" \
     "src/PetPal.App.Ui/wwwroot/fonts/*" tests/PetPal.App.Tests/StyleSheetTests.cs
c 15 "Karkas" "Üst panel" "Alt naviqasiya" "Kartlar" "Düymələr" "Formalar" \
     "Progress" "Vəziyyət ekranları" "Animasiyalar" "Sətir daxilindəki düymələr"
f 16 src/PetPal.App.Ui/Pages/Login.razor src/PetPal.App.Ui/Pages/Register.razor \
     src/PetPal.App.Ui/Components/Shared/LoadingState.razor
f 17 src/PetPal.App.Ui/Pages/Profiles.razor
c 17 "Profil seçimi" "PIN klaviaturası" "Pet növünün seçimi"
f 18 src/PetPal.App.Ui/Pages/Home.razor src/PetPal.App.Ui/Components/Shared/PetGauge.razor \
     src/PetPal.App.Ui/Components/Shared/StatBar.razor \
     src/PetPal.App.Ui/Components/Shared/GoalBar.razor
c 18 "Tam ekranlı səhnə (Ev)" "Səhnə qəhrəmanı" \
     "Fəaliyyət düymələri (Learn / Play / Care / Explore)" "Danışıq balonu" \
     "Stat barlar (care ekranı)"

# ============================ Faza 3 — Pet və qulluq ============================

f 19 src/PetPal.App.Ui/Components/Shared/PetAvatar.razor \
     src/PetPal.App.Ui/Components/Shared/PetBody.razor \
     src/PetPal.App.Ui/wwwroot/css/pet.css
c 19 "Pet səhnəsi"
f 20 "src/PetPal.Api/Migrations/20260816172305_PetHatching*" \
     tests/PetPal.Tests/PetProgressionTests.cs
f 21 src/PetPal.App.Ui/Pages/Care.razor tests/PetPal.Tests/PetCareTests.cs \
     tests/PetPal.App.Tests/CareScreenMarkupTests.cs
c 21 "Qulluq səhnəsi" "Qulluq dock-u" "Qulluq ölçərləri (halqa)"
f 22 src/PetPal.App.Ui/wwwroot/js/petCare.js
c 23 "Yem qabı" "Yemək otağı"
c 24 "Yataq otağı" "Yataq" "Hamam" "Pet-in otağı (Ev)"
f 25 "src/PetPal.Api/Migrations/20260818213932_PetAccessoryEquipping*" \
     tests/PetPal.Tests/PetAccessoryTests.cs
c 25 "Paltar dəyişmə otağı" "Şkafda növ seçimi"

# ============================ Faza 4 — Oyunlar ============================

f 26 src/PetPal.App.Ui/Components/Shared/GameShell.razor \
     src/PetPal.App.Ui/Components/Shared/GameLives.razor \
     src/PetPal.App.Ui/Components/Shared/GameHost.cs \
     src/PetPal.App.Ui/Components/Shared/GamePreview.razor \
     src/PetPal.App.Ui/Pages/Play.razor tests/PetPal.App.Tests/GameMarkupTests.cs \
     tests/PetPal.Tests/GameAndSprintTests.cs
c 26 "Oyun HUD-u (maketdən)" "Oyun çərçivəsi" "Səhnə fonları" "Oyun canları" \
     "Oyun nəticəsi" "Oyun kafelləri"
f 27 "src/PetPal.Api/Migrations/20260817165842_UnlockableGames*"
f 28 src/PetPal.App.Ui/Components/Shared/MemoryMatchGame.razor
c 28 "Yaddaş cütləri"
f 29 src/PetPal.App.Ui/Components/Shared/QuickTapGame.razor
c 29 "Sürətli hesab"
f 30 src/PetPal.App.Ui/Components/Shared/BubblePopGame.razor \
     tests/PetPal.App.Tests/BubblePopMarkupTests.cs
c 30 "Baloncuq ovu"
f 31 src/PetPal.App.Ui/Components/Shared/ColorEchoGame.razor
c 31 "Rəng sırası"
f 32 src/PetPal.App.Ui/Components/Shared/StarRunGame.razor
c 32 "Ulduz qaçışı"
f 33 src/PetPal.App.Ui/Components/Shared/FruitSliceGame.razor
c 33 "Meyvə kəs"
f 34 src/PetPal.App.Ui/Components/Shared/BasketCatchGame.razor
c 34 "Səbət tut"
f 35 src/PetPal.App.Ui/Components/Shared/CloudJumpGame.razor
c 35 "Bulud tullanışı"
f 36 src/PetPal.App.Ui/Components/Shared/LetterHuntGame.razor
c 36 "Hərf ovu"
f 37 src/PetPal.App.Ui/Components/Shared/ChefOrderGame.razor \
     src/PetPal.App.Ui/wwwroot/js/gameDrag.js
c 37 "Oyunda sürüklənən əşya" "Balaca aşpaz"

# ============================ Faza 5 — Öyrənmə və dünya ============================

f 38 src/PetPal.App.Ui/Pages/Learn.razor tests/PetPal.Tests/LearningFlowTests.cs \
     tests/PetPal.Tests/AdaptiveEngineTests.cs tests/PetPal.Tests/QuestionBankTests.cs
c 38 "Sual"
f 39 src/PetPal.App.Ui/Pages/ProgressPage.razor \
     src/PetPal.App.Ui/Components/Shared/MissionRow.razor
c 39 "Missiya sətri" "Nişanlar"
f 40 src/PetPal.App.Ui/Pages/World.razor tests/PetPal.Tests/WorldStateTests.cs \
     tests/PetPal.Tests/WorldMissionTests.cs
c 40 "Xəritə başlığı (Dünya)" "Dünya zonaları"
f 41 "src/PetPal.Api/Migrations/20260804143902_LocalizationGamesAndScreenTime*" \
     tests/PetPal.Tests/LocalizationTests.cs tests/PetPal.App.Tests/LocTests.cs

# ============================ Faza 6 — Kəşf, AI və söhbət ============================

f 42 "src/PetPal.Api/Discoveries/*" "src/PetPal.Shared/Dtos/Discovery/*" \
     tests/PetPal.Tests/DiscoveryAndSocialTests.cs
f 43 src/PetPal.App.Ui/Pages/Explore.razor \
     src/PetPal.App.Ui/Components/Shared/DiscoveryList.razor \
     tests/PetPal.App.Tests/ExploreScreenMarkupTests.cs
c 43 "Kəşf kolleksiyası" "Kəşf formasının şəkil önizləməsi"
f 44 "src/PetPal.Api/Ai/*" tests/PetPal.Tests/PetVoiceAiTests.cs
# Ai qatı gəldi — ev ekranının xidməti öz əslinə qayıdır.
fx 44 src/PetPal.Api/Home/HomeService.cs
f 45 "src/PetPal.Api/Migrations/20260822194315_PetChat*" \
     src/PetPal.Shared/Dtos/Pets/PetChatDtos.cs tests/PetPal.Tests/PetChatTests.cs
f 46 src/PetPal.App.Ui/Pages/Chat.razor
f 47 src/PetPal.Api/Pets/PetVoice.cs
# Səs qatı gəldi — endpoint-lər öz əslinə qayıdır.
fx 47 src/PetPal.Api/Pets/PetEndpoints.cs

# ============================ Faza 7 — Sosial və arena ============================

f 48 "src/PetPal.Api/Realtime/*"
w 49 src/PetPal.Api/Social/SocialService.cs
f 49 "src/PetPal.Api/Social/*" "src/PetPal.Shared/Dtos/Social/*"
f 50 "src/PetPal.Api/Migrations/20260906110727_SocialConsentAndPresence*" \
     tests/PetPal.Tests/SocialRegressionTests.cs
f 51 src/PetPal.App.Ui/Pages/Friends.razor \
     src/PetPal.App.Ui/Components/Shared/FriendsPanel.razor
c 51 "Dostlar" "Onlayn nişanı"
f 52 "src/PetPal.Api/Notifications/*" "src/PetPal.Shared/Dtos/Notifications/*" \
     "src/PetPal.Api/Migrations/20260906114150_DeviceTokens*" \
     tests/PetPal.Tests/NotificationAndStorageTests.cs
# Bildirişlər gəldi — sosial xidmət öz əslinə qayıdır.
fx 52 src/PetPal.Api/Social/SocialService.cs
f 53 "src/PetPal.Api/Learning/Arena/*" \
     "src/PetPal.Api/Migrations/20260822212159_BilikArenasi*" \
     "src/PetPal.Api/Migrations/20260822214506_ArenaMesqRejimi*" \
     src/PetPal.Shared/Dtos/Learning/ArenaDtos.cs tests/PetPal.Tests/ArenaTests.cs
f 54 "src/PetPal.Api/Migrations/20260823083227_ArenaSuretBonusu*" \
     tests/PetPal.Tests/ArenaLeagueTests.cs
# Arena gəldi — mükafat və öyrənmə xidmətləri öz əsillərinə qayıdır.
fx 54 src/PetPal.Api/Rewards/RewardService.cs src/PetPal.Api/Learning/LearningService.cs
f 55 "src/PetPal.Api/Migrations/20260823094112_SinxronDuel*" \
     src/PetPal.App.Ui/Pages/Arena.razor
c 55 "Hesab lövhəsi" "Duel gedərkən rəqib zolağı" "Sual-sual müqayisə" \
     "Rəqib axtarışı" "Başlanğıc geri sayımı" "Nəticənin hökmü" \
     "Cavabdan sonrakı fasilə" "Həftəlik liqa" "Arena tabları: Yarış · Dostlar"

# ============================ Faza 8 — Valideyn və platforma ============================

f 56 "src/PetPal.Api/Parent/*" "src/PetPal.Shared/Dtos/Parent/*" \
     src/PetPal.App.Ui/Pages/ParentDashboard.razor \
     tests/PetPal.Tests/ParentDashboardTests.cs tests/PetPal.Tests/ScreenTimeTests.cs \
     tests/PetPal.App.Tests/AppSessionTests.cs
c 56 "Valideyn paneli" "Ekran vaxtı blok ekranı" "Sprint təklifi" \
     "Dil seçimi (valideyn bölməsi)"
# Sonuncu qat da bağlandı: Program.cs və app.css əsli ilə əvəz olunur.
fx 56 src/PetPal.Api/Program.cs src/PetPal.App.Ui/wwwroot/css/app.css
f 57 src/PetPal.App.Ui/wwwroot/js/appFrame.js src/PetPal.App.Ui/wwwroot/js/share.js \
     tests/PetPal.App.Tests/ResponsiveLayoutTests.cs
f 58 "src/PetPal.Web/*" railway.web.json tests/PetPal.App.Tests/WebHostCacheTests.cs
f 59 "src/PetPal.App/*" "src/PetPal.Launcher/*" "scripts/dev/*"
f 60 "docs/*" README.md railway.api.json "scripts/railway/*" \
     src/PetPal.Api/Migrations/AppDbContextModelSnapshot.cs \
     "design-canvas/*" "design-games/*" .tmp-visual-agent-bed.html debug.log

# Program.cs demək olar hər API addımında böyüyür: qat gələndə ona iki sətir
# əlavə olunur (`AddScoped<...>` və `MapXEndpoints()`). Bunu unutmaq build-i
# qırmır — daha pisi, endpoint SƏSSİZCƏ qeydiyyatdan keçmir. Ona görə hər
# belə addımda xatırladıcı var.
for s in 5 6 7 8 9 10 12 13 42 44 48 49 52 53; do
    w "$s" src/PetPal.Api/Program.cs
done

sort -n -k1 -s "$TMP" > "$OUT"

# ============================ Əhatənin yoxlanması ============================

lines=$(wc -l < "$OUT")
nfile=$(awk -F'\t' '$2=="file"' "$OUT" | wc -l)
ncss=$(awk -F'\t' '$2=="css"' "$OUT" | wc -l)
nwrite=$(awk -F'\t' '$2=="write"' "$OUT" | wc -l)

echo "Manifest: docs/bootcamp/manifest.tsv"
echo "  sətir: $lines  (file: $nfile, css: $ncss, write: $nwrite)"

awk -F'\t' '$2=="file"{print $3}' "$OUT" | sort > "$TMP.assigned"
git ls-files | sort > "$TMP.all"

missing=$(comm -23 "$TMP.all" "$TMP.assigned" | wc -l)
dupes=$(awk -F'\t' '$2=="file"{print $3}' "$OUT" | sort | uniq -d | wc -l)
sections=$(grep -c '^/\* ---------- ' src/PetPal.App.Ui/wwwroot/css/app.css)

echo
echo "── Heç bir addıma düşməyən fayl: $missing"
[ "$missing" -gt 0 ] && comm -23 "$TMP.all" "$TMP.assigned" | sed 's/^/    /'

echo "── İki addıma birdən düşən fayl: $dupes"
[ "$dupes" -gt 0 ] && awk -F'\t' '$2=="file"{print $3}' "$OUT" | sort | uniq -d | sed 's/^/    /'

echo "── app.css bölmələri: manifestdə $ncss, faylda $sections"

exit 0
