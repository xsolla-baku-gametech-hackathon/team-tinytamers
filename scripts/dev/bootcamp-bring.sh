#!/usr/bin/env bash
#
# Bootcamp replay — bir addımın işini hədəf repoya gətirir.
#
# İşi manifest deyir (`docs/bootcamp/manifest.tsv`), skript isə onu icra edir:
#
#   file   — faylı olduğu kimi köçürür
#   css    — app.css-in həmin bölməsini hədəfdəki app.css-ə əlavə edir
#   write  — köçürmür, sadəcə XƏBƏRDARLIQ edir: bu fayl əl ilə yazılmalıdır
#
# Commit bu skriptin işi deyil: mesaj hər addım üçün ayrıca yazılır, ona görə
# köçürmə ilə commit qəsdən ayrılıb.
#
#   ./scripts/dev/bootcamp-bring.sh --target DIR --step 7
#   ./scripts/dev/bootcamp-bring.sh --target DIR --step 7 --dry-run
#
set -euo pipefail

SOURCE="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MANIFEST="$SOURCE/docs/bootcamp/manifest.tsv"
CSS_REL="src/PetPal.App.Ui/wwwroot/css/app.css"
TARGET=""
STEP=""
DRY=0

while [ $# -gt 0 ]; do
    case "$1" in
        --source) SOURCE="$2"; shift 2 ;;
        --target) TARGET="$2"; shift 2 ;;
        --step)   STEP="$2";   shift 2 ;;
        --dry-run) DRY=1;      shift ;;
        *) echo "Naməlum arqument: $1" >&2; exit 2 ;;
    esac
done

[ -n "$TARGET" ] || { echo "--target verilməyib." >&2; exit 2; }
[ -n "$STEP" ]   || { echo "--step verilməyib." >&2; exit 2; }
[ -d "$TARGET" ] || { echo "Hədəf qovluq yoxdur: $TARGET" >&2; exit 2; }
[ -f "$MANIFEST" ] || { echo "Manifest yoxdur: $MANIFEST" >&2; exit 2; }

rows=$(awk -F'\t' -v s="$STEP" '$1==s' "$MANIFEST")
[ -n "$rows" ] || { echo "Addım $STEP üçün manifestdə sətir yoxdur." >&2; exit 1; }

# app.css-in bir bölməsini çıxarır: başlıq şərhindən NÖVBƏTİ başlıq şərhinə
# qədər. Bölmələr faylda ardıcıldır, ona görə kəsim sərhədi şərhin özüdür.
extract_css() {
    local name="$1"
    awk -v want="$name" '
        /^\/\* -{5,} / {
            line = $0
            sub(/^\/\* -+ /, "", line)
            sub(/ -+.*$/, "", line)
            inside = (line == want)
        }
        inside { print }
    ' "$SOURCE/$CSS_REL"
}

files=0; sections=0; writes=0

while IFS=$'\t' read -r step kind target; do
    case "$kind" in
        file)
            src="$SOURCE/$target"
            [ -e "$src" ] || { echo "  Mənbədə yoxdur: $target" >&2; exit 1; }

            if [ "$DRY" = 0 ]; then
                mkdir -p "$TARGET/$(dirname "$target")"
                cp -r "$src" "$TARGET/$target"
            fi

            printf '  + %-64s %s sətir\n' "$target" "$(wc -l < "$src" 2>/dev/null || echo '-')"
            files=$((files + 1))
            ;;
        css)
            body=$(extract_css "$target")
            [ -n "$body" ] || { echo "  app.css-də belə bölmə yoxdur: $target" >&2; exit 1; }

            if [ "$DRY" = 0 ]; then
                mkdir -p "$TARGET/$(dirname "$CSS_REL")"
                printf '%s\n' "$body" >> "$TARGET/$CSS_REL"
            fi

            printf '  ~ app.css: %-52s %s sətir\n' "$target" "$(printf '%s\n' "$body" | wc -l)"
            sections=$((sections + 1))
            ;;
        write)
            printf '  ! ƏL İLƏ YAZILIR: %s\n' "$target"
            writes=$((writes + 1))
            ;;
    esac
done <<< "$rows"

echo "  ————  $files fayl, $sections css bölməsi, $writes əl işi"
[ "$DRY" = 1 ] && echo "  (dry-run — heç nə yazılmadı)"
exit 0
