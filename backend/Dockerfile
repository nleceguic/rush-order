# Build context: backend/
# Production:   docker build -t rushorder-api .
# Dev target:   docker build --target development -t rushorder-api:dev .

# ─── Stage: base ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app

# curl para HEALTHCHECK; icu-libs para culturas (es-ES) — la imagen alpine
# base viene en modo "globalization-invariant" y sin ICU por defecto, lo que
# rompe CultureInfo.GetCultureInfo("es-ES") usado en WeeklyInsightsJob.
RUN apk add --no-cache curl icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# La imagen base aspnet:8.0-alpine ya trae un usuario no-root "app" preconfigurado

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# ── Variables de entorno esperadas (sobreescribir en runtime) ─────────────────
# Entorno de ejecución: Production | Staging | Development
ENV ASPNETCORE_ENVIRONMENT=Production
# Cadena de conexión PostgreSQL
ENV Database__ConnectionString=""
# Cadena de conexión Redis  (ej: redis:6379)
ENV Redis__ConnectionString=""
# Emisor del token JWT
ENV Jwt__Issuer=""
# Audiencia del token JWT
ENV Jwt__Audience=""

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# ─── Stage: build ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar sólo los .csproj primero → Docker cachea la capa de restore
# si el código cambia pero las dependencias no lo hacen.
COPY src/RushOrder.Domain/RushOrder.Domain.csproj             src/RushOrder.Domain/
COPY src/RushOrder.Application/RushOrder.Application.csproj   src/RushOrder.Application/
COPY src/RushOrder.Infrastructure/RushOrder.Infrastructure.csproj src/RushOrder.Infrastructure/
COPY src/RushOrder.API/RushOrder.API.csproj                   src/RushOrder.API/

RUN dotnet restore src/RushOrder.API/RushOrder.API.csproj \
    --runtime linux-musl-x64 \
    -p:PublishReadyToRun=true

# Copiar código y publicar con ReadyToRun para mejor startup time
COPY . .
RUN dotnet publish src/RushOrder.API/RushOrder.API.csproj \
    -c Release \
    -r linux-musl-x64 \
    --self-contained false \
    --no-restore \
    -p:PublishReadyToRun=true \
    -o /app/publish

# ─── Stage: final (producción) ───────────────────────────────────────────────
FROM base AS final

COPY --from=build --chown=app:app /app/publish .

# Pre-crear con dueño "app" — si un volumen se monta aquí, Docker copia este
# directorio (vacío, con estos permisos) al volumen en su primera creación.
RUN mkdir -p keys logs && chown app:app keys logs

USER app

ENTRYPOINT ["dotnet", "RushOrder.API.dll"]

# ─── Stage: development (hot-reload con dotnet watch) ────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS development
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_USE_POLLING_FILE_WATCHER=1

# Pre-restore para acelerar el primer arranque.
# El volumen montado en docker-compose sobreescribe este código en runtime.
COPY src/RushOrder.Domain/RushOrder.Domain.csproj             src/RushOrder.Domain/
COPY src/RushOrder.Application/RushOrder.Application.csproj   src/RushOrder.Application/
COPY src/RushOrder.Infrastructure/RushOrder.Infrastructure.csproj src/RushOrder.Infrastructure/
COPY src/RushOrder.API/RushOrder.API.csproj                   src/RushOrder.API/
RUN dotnet restore src/RushOrder.API/RushOrder.API.csproj

CMD ["dotnet", "watch", "run", "--project", "src/RushOrder.API", "--no-restore", "--no-launch-profile"]
