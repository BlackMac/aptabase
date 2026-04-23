# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Aptabase is an open-source, privacy-first analytics platform for mobile, desktop, and web apps. It consists of an ASP.NET Core 8 backend (C#) and a React 18 frontend (TypeScript), with PostgreSQL for relational data and ClickHouse for analytics event storage.

## Development Commands

### Setup
```bash
docker compose up -d                    # Start PostgreSQL, ClickHouse, Mailcatcher
cd src && npm install && dotnet restore  # Install dependencies
cp src/Properties/launchSettings.example.json src/Properties/launchSettings.json
```

### Running (from repo root)
```bash
make dev           # Run both backend and frontend in watch mode
make dotnet-dev    # Backend only (dotnet watch with DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1)
make vite-dev      # Frontend only (Vite dev server)
```

The frontend runs at `https://localhost:3000` and proxies `/api`, `/webhook`, `/uploads` to the .NET backend at `https://localhost:5251`.

### Testing
```bash
dotnet test                                          # Run all tests (unit + integration)
dotnet test tests/UnitTests/                         # Unit tests only
dotnet test tests/IntegrationTests/                  # Integration tests only
dotnet test --filter "FullyQualifiedName~ClassName"  # Run specific test class
```

Tests use xUnit + FluentAssertions + Moq. Integration tests require the Docker services running.

### Build
```bash
cd src && npm run build    # Frontend build (outputs to src/wwwroot/)
dotnet build               # Backend build
```

## Architecture

### Data Flow
```
SDKs → POST /api/events (with app-key header)
  → Event parsing + privacy hashing + GeoIP lookup
  → InMemoryEventBuffer
  → EventBackgroundWritter flushes to ClickHouse/Tinybird
  → Query clients serve aggregated data to React dashboard
```

### Backend (src/Features/)
Feature-based organization. Each feature folder contains controllers, services, and queries:

- **Ingestion** — Event ingestion API, user agent parsing, in-memory event buffer, background writer to ClickHouse/Tinybird
- **Stats** — Analytics query layer with two implementations: `ClickHouseQueryClient` and `TinybirdQueryClient` behind `IQueryClient`
- **Authentication** — Cookie-based auth with OAuth providers (GitHub, Google)
- **Privacy** — Daily-rotating salt for user ID hashing (GDPR compliance)
- **Billing** — LemonSqueezy integration, trial/overuse cron jobs (enabled only for managed cloud or dev)
- **Apps** — App CRUD and management
- **GeoIP** — MaxMind database or cloud-based geolocation
- **Notification** — Email via SMTP/MailKit (dev uses Mailcatcher at `http://localhost:1080`)

Key pattern: ClickHouse vs Tinybird is chosen at startup based on whether `CLICKHOUSE_URL` env var is set. Self-hosted uses ClickHouse; managed cloud can use Tinybird.

### Frontend (src/webapp/)
- **Build**: Vite + React SWC, outputs to `src/wwwroot/`
- **State**: Jotai (atoms) + TanStack React Query (server state)
- **Styling**: Tailwind CSS + Radix UI primitives
- **Charts**: Chart.js
- **Path aliases**: `@components`, `@features`, `@hooks`, `@fns` → `src/webapp/{name}/`

### Database
- **PostgreSQL 15**: Users, apps, subscriptions, migrations (FluentMigrator, `src/Data/Migrations/`)
- **ClickHouse**: Analytics events, schemas in `etc/clickhouse/`
- Dapper is used for data access with `MatchNamesWithUnderscores = true`

### Environment Configuration
Environment variables are loaded via `EnvSettings.cs` (not appsettings.json). Required vars: `BASE_URL`, `DATABASE_URL`, `AUTH_SECRET`. The `REGION` variable controls mode: `EU`/`US` = managed cloud, `DEV` = development, `SH` = self-hosted.
