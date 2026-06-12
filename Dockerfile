# ─── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Restaurar dependencias por separado para aprovechar la caché de Docker
COPY rush-order.sln .
COPY backend/src/RushOrder.Domain/RushOrder.Domain.csproj             backend/src/RushOrder.Domain/
COPY backend/src/RushOrder.Application/RushOrder.Application.csproj   backend/src/RushOrder.Application/
COPY backend/src/RushOrder.Infrastructure/RushOrder.Infrastructure.csproj backend/src/RushOrder.Infrastructure/
COPY backend/src/RushOrder.API/RushOrder.API.csproj                   backend/src/RushOrder.API/
RUN dotnet restore backend/src/RushOrder.API/RushOrder.API.csproj

# Copiar el resto y publicar
COPY . .
RUN dotnet publish backend/src/RushOrder.API/RushOrder.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Usuario no-root para seguridad
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RushOrder.API.dll"]
