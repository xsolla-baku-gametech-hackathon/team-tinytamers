# Build mərhələsi
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore-u ayrıca layer-də saxlayırıq ki, yalnız csproj dəyişəndə yenidən işləsin.
COPY src/PetPal.Shared/PetPal.Shared.csproj src/PetPal.Shared/
COPY src/PetPal.Api/PetPal.Api.csproj src/PetPal.Api/
RUN dotnet restore src/PetPal.Api/PetPal.Api.csproj

COPY src/PetPal.Shared/ src/PetPal.Shared/
COPY src/PetPal.Api/ src/PetPal.Api/
RUN dotnet publish src/PetPal.Api/PetPal.Api.csproj -c Release -o /app --no-restore

# Runtime mərhələsi
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Root altında işləməmək — konteyner təhlükəsizliyinin ən sadə və təsirli addımı.
# .NET 8-dən bəri rəsmi image-lərdə hazır root-olmayan istifadəçi var (app, uid 1654),
# ona görə ayrıca hesab yaradılmır — bazadakı $APP_UID istifadə olunur.
RUN mkdir -p /app/uploads && chown -R $APP_UID:$APP_UID /app
USER $APP_UID

COPY --from=build --chown=$APP_UID:$APP_UID /app ./

# Port ASPNETCORE_URLS ilə deyil, PORT ilə verilir: Railway/Render kimi platformalar
# portu öz dəyişəni ilə diktə edir və Program.cs onu oxuyur. Lokalda defolt 8080 qalır.
ENV PORT=8080
EXPOSE 8080

# HEALTHCHECK qəsdən yoxdur: runtime image-də wget/curl gəlmir və orkestrator
# (Azure Container Apps) onsuz da /health/live və /health/ready probe-larını özü vurur.

ENTRYPOINT ["dotnet", "PetPal.Api.dll"]
