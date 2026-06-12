# Rush Order

Monorepo con arquitectura Clean Architecture (.NET 8) + PWA (React 18 + Vite) + Desktop.

## Estructura

```
rush-order/
├── backend/
│   ├── src/
│   │   ├── RushOrder.Domain/          # Entidades, value objects, interfaces
│   │   ├── RushOrder.Application/     # Casos de uso, CQRS (MediatR)
│   │   ├── RushOrder.Infrastructure/  # EF Core, repositorios, servicios externos
│   │   └── RushOrder.API/             # ASP.NET Core Web API
│   └── tests/
│       ├── RushOrder.Domain.Tests/
│       ├── RushOrder.Application.Tests/
│       └── RushOrder.API.IntegrationTests/
├── desktop/
│   └── src/
│       ├── RushOrder.Desktop/
│       └── RushOrder.Desktop.Core/
├── pwa/                               # Vite + React 18 + TypeScript
├── infrastructure/
│   ├── terraform/
│   └── docker/
└── .github/workflows/
```

## Requisitos

| Herramienta | Versión mínima |
|-------------|----------------|
| .NET SDK    | 8.0            |
| Node.js     | 20 LTS         |
| npm         | 10             |

## Setup rápido

### Backend

```bash
# Restaurar dependencias y compilar
dotnet restore rush-order.sln
dotnet build rush-order.sln

# Ejecutar API
cd backend/src/RushOrder.API
dotnet run

# Tests
dotnet test rush-order.sln
```

### PWA

```bash
cd pwa
npm install
npm run dev        # http://localhost:5173
npm run build      # output en dist/
```

### Docker (desarrollo)

```bash
docker compose -f infrastructure/docker/docker-compose.yml up -d
```

## Variables de entorno

Copia `appsettings.Development.json.example` a `appsettings.Development.json` y configura:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=RushOrder;..."
  },
  "Jwt": {
    "Key": "...",
    "Issuer": "rush-order-api"
  }
}
```

## Arquitectura de capas

```
API → Infrastructure → Application → Domain
```

- **Domain**: sin dependencias externas. Entidades, eventos de dominio, interfaces de repositorio.
- **Application**: depende solo de Domain. Comandos/queries MediatR, validaciones FluentValidation.
- **Infrastructure**: depende de Application. Implementaciones concretas (EF Core, HTTP clients).
- **API**: depende de Infrastructure. Controllers/minimal endpoints, middleware, Swagger.
