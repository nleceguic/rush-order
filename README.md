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

## Quick Start

### 1. Requisitos previos

| Herramienta | Versión mínima | Necesario para        |
|-------------|----------------|-----------------------|
| .NET SDK    | 8.0            | Backend               |
| Node.js     | 20 LTS         | PWA                   |
| npm         | 10             | PWA                   |
| Docker      | 24             | Infraestructura local  |
| Docker Compose | 2.x         | Infraestructura local  |

### 2. Levantar la infraestructura

```bash
# Primera vez: copia las variables de entorno
cp .env.example .env

# Levantar todos los servicios (postgres, redis, pgadmin, mailhog)
bash infrastructure/docker/start-dev.sh

# Reset completo (elimina volúmenes y reinicia desde cero)
bash infrastructure/docker/reset-dev.sh
```

### 3. URLs de acceso

| Servicio        | URL / Host                          | Credenciales                          |
|-----------------|-------------------------------------|---------------------------------------|
| API (Swagger)   | http://localhost:5000/swagger        | —                                     |
| PWA             | http://localhost:5173                | —                                     |
| pgAdmin         | http://localhost:5050                | admin@rushorder.local / admin         |
| MailHog UI      | http://localhost:8025                | —                                     |
| PostgreSQL      | localhost:5432                       | rushorder / rushorder_dev_pass        |
| Redis           | localhost:6379                       | sin autenticación (solo dev)          |
| MailHog SMTP    | localhost:1025                       | —                                     |

---

## Setup de desarrollo

### Backend

```bash
# Restaurar dependencias y compilar
dotnet restore rush-order.sln
dotnet build rush-order.sln

# Ejecutar API (requiere infraestructura Docker activa)
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

## Variables de entorno

Copia `.env.example` a `.env` en la raíz del proyecto y ajusta los valores si es necesario.
El archivo `backend/src/RushOrder.API/appsettings.Development.json` ya apunta a los servicios
Docker con las credenciales por defecto.

## Branch Protection Rules

Reglas recomendadas para `main` y `develop` (configurar en GitHub → Settings → Branches):

### `main`
| Regla | Valor |
|-------|-------|
| Require pull request before merging | ✅ — 1 approval mínimo |
| Require status checks to pass | ✅ — `backend-ci`, `pwa-ci` |
| Require branches to be up to date | ✅ |
| Require conversation resolution | ✅ |
| Restrict force pushes | ✅ |
| Restrict deletions | ✅ |
| Require CODEOWNERS review | ✅ |

### `develop`
| Regla | Valor |
|-------|-------|
| Require pull request before merging | ✅ — 1 approval |
| Require status checks to pass | ✅ — `backend-ci`, `pwa-ci` |
| Restrict force pushes | ✅ |

> Los status checks se activan automáticamente tras la primera ejecución del pipeline.

## Arquitectura de capas

```
API → Infrastructure → Application → Domain
```

- **Domain**: sin dependencias externas. Entidades, eventos de dominio, interfaces de repositorio.
- **Application**: depende solo de Domain. Comandos/queries MediatR, validaciones FluentValidation.
- **Infrastructure**: depende de Application. Implementaciones concretas (EF Core, HTTP clients).
- **API**: depende de Infrastructure. Controllers/minimal endpoints, middleware, Swagger.
