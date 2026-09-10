# Build mərhələsi — Blazor WebAssembly publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore ayrıca layer-də: yalnız csproj dəyişəndə yenidən işləyir.
COPY src/PetPal.Shared/PetPal.Shared.csproj src/PetPal.Shared/
COPY src/PetPal.App.Ui/PetPal.App.Ui.csproj src/PetPal.App.Ui/
COPY src/PetPal.Web/PetPal.Web.csproj src/PetPal.Web/
RUN dotnet restore src/PetPal.Web/PetPal.Web.csproj

COPY src/PetPal.Shared/ src/PetPal.Shared/
COPY src/PetPal.App.Ui/ src/PetPal.App.Ui/
COPY src/PetPal.Web/ src/PetPal.Web/
RUN dotnet publish src/PetPal.Web/PetPal.Web.csproj -c Release -o /app --no-restore

# Runtime mərhələsi — statik fayllar üçün nginx (root olmayan variant)
FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime

# templates/ altındakı fayllar nginx-in öz entrypoint-i tərəfindən envsubst ilə
# emal olunub conf.d/-ə yazılır — beləliklə ${PORT} start zamanı doldurulur.
COPY --chown=nginx:nginx src/PetPal.Web/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --chown=nginx:nginx --chmod=755 src/PetPal.Web/nginx-init-api-base-url.sh /docker-entrypoint.d/10-petpal-api-base-url.sh
COPY --from=build --chown=nginx:nginx /app/wwwroot /usr/share/nginx/html

# Hazır sıxılmış nüsxələr yalnız MƏZMUNU DƏYİŞMƏYƏN fayllar üçün təhlükəsizdir.
#
# appsettings.json start zamanı yenidən yazılır (bax 10-petpal-api-base-url.sh),
# publish isə onun .gz/.br nüsxələrini də yaradır. Onlar qalsa, gzip_static
# brauzerə KÖHNƏ məzmunu verər — yəni app səhv API ünvanına baxar.
#
# index.html eyni tələnin növbəti qurbanıdır: brauzer sıxılmış nüsxəni alır,
# ona görə fayla edilən hər hansı start-vaxtı düzəliş səssizcə itir. Bu, real
# olaraq bir dəfə diaqnozu gizlədib — düz fayl düzəlmiş, brauzer isə köhnəsini
# almışdı. İkisi də bir neçə kilobaytdır; nginx onları onsuz da anında sıxır.
USER root
RUN rm -f /usr/share/nginx/html/appsettings.json.gz /usr/share/nginx/html/appsettings.json.br \
          /usr/share/nginx/html/index.html.gz /usr/share/nginx/html/index.html.br
USER 101

ENV PORT=8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --spider -q "http://localhost:${PORT}/health/live" || exit 1
