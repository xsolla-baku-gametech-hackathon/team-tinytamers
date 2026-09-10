#!/bin/sh
# API ünvanı image-ə "yandırılmır" — start zamanı environment variable-dan yazılır ki,
# eyni image həm staging, həm produksiya üçün işləsin.
set -e

if [ -n "$PETPAL_API_BASE_URL" ]; then
    # Yalnız faylın özü yazılır — hazır sıxılmış .gz/.br nüsxələri build zamanı
    # silinib (bax Dockerfile), yoxsa nginx brauzerə köhnə məzmunu verərdi.
    printf '{ "ApiBaseUrl": "%s" }\n' "$PETPAL_API_BASE_URL" > /usr/share/nginx/html/appsettings.json
    echo "petpal: ApiBaseUrl = $PETPAL_API_BASE_URL"
else
    echo "petpal: PETPAL_API_BASE_URL teyin olunmayib - appsettings.json-dakı defolt qalır." >&2
fi
