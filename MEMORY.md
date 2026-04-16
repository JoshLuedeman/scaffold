# Project Memory

This file captures project learnings that persist across agent sessions.

## Project Overview

**Scaffold** is a .NET 8 + React 19 web application for assessing, planning, and executing SQL Server database migrations to Azure SQL services. It uses a pluggable engine architecture designed to support multiple source/target database platforms.

- **Repository**: joshluedeman/scaffold
- **License**: MIT
- **CI**: GitHub Actions (`.github/workflows/ci.yml`)
- **Deployment**: Azure Developer CLI (`azd up`) — Container App (API) + Static Web App (frontend)

## Architecture

### Backend (.NET 8)

| Project | Purpose | Dependencies |
|---|---|---|
| `Scaffold.Core` | Domain models, interfaces, enums | None |
| `Scaffold.Assessment` | Assessment engines (schema, data, perf, compatibility, pricing, tier recommendation) | Core |
| `Scaffold.Migration` | Migration engines (DACPAC deploy, SqlBulkCopy, change tracking sync, script execution, validation) | Core |
| `Scaffold.Infrastructure` | EF Core `ScaffoldDbContext`, repositories, Azure SDK wrappers | Core |
| `Scaffold.Api` | ASP.NET Core Web API, SignalR hub, scheduler service, DTOs | All |

### Frontend

| Project | Purpose |
|---|---|
| `Scaffold.Web` | React 19, TypeScript 5.9, Vite, Fluent UI v9, MSAL auth, SignalR client |

### Test Projects

| Project | Framework | Focus |
|---|---|---|
| `Scaffold.Assessment.Tests` | xUnit + Moq | Assessors, pricing, compatibility, tier recommender |
| `Scaffold.Api.Tests` | xUnit + WebApplicationFactory + in-memory EF Core | Controllers, auth, scheduler, progress |
| `Scaffold.Migration.Tests` | xUnit + Moq | Migrator, schema deployer, data copier, scripts |
| `Scaffold.Integration.Tests` | xUnit + real SQL Server | End-to-end against live database (CI main-only) |

## Key Technical Decisions

- **EF Core JSON columns**: Complex types (Schema, DataProfile, Performance, CompatibilityIssues, Recommendation, Scripts) stored via `ToJson()`
- **Enums stored as strings** in the database
- **No circular references**: Assessment and Migration must NOT reference each other; both reference only Core
- **Auth**: Microsoft Entra ID (production) / `DevAuthHandler` with `DisableAuth=true` (development)
- **Real-time**: SignalR `MigrationHub` broadcasts progress and persists to `MigrationProgressRecords`
- **Background scheduler**: `MigrationSchedulerService` polls every 30s for approved plans
- **Docker Compose**: SQL Server 2025 + API + Nginx (runtime DNS resolution with `resolver 127.0.0.11`)

## Build & Test Commands

```bash
dotnet build                                        # Build entire solution
dotnet test                                         # Run all 211+ tests
dotnet test --filter "FullyQualifiedName~TestName"  # Single test
dotnet run --project src/Scaffold.Api               # API on localhost:5062
cd src/Scaffold.Web && npm install && npm run dev   # Frontend on localhost:5173
npx tsc --noEmit                                    # TypeScript check
docker compose up --build                           # Full stack locally
azd up                                              # Deploy to Azure
```

## Current State & Roadmap

### Milestones (all issues open)

| Phase | Focus | Issues |
|---|---|---|
| **Phase 0**: Foundation & Production Readiness | Multi-platform refactoring, error handling, health checks | #1–#11 |
| **Phase 1**: PostgreSQL Assessment Engine | Npgsql, PG schema/data/perf analysis, compatibility, pricing | #12–#23 |
| **Phase 2**: SQL Server → PostgreSQL Migration | Data type mapping, DDL translation, schema deploy, bulk data | #24–#31 |
| **Phase 3**: PostgreSQL → Azure PG Migration | PG schema extractor, data copier, logical replication | #32–#37 |
| **Phase 4**: UI & API Multi-Platform | Controllers, TypeScript types, platform selector, PG progress | #38–#43 |
| **Phase 5**: Documentation & Release Prep | ADR, README, API docs, Docker Compose PG, MEMORY.md | #44–#48 |

### Phase 0 Dependency Chain
`#2 DatabasePlatform enum` → `#3 ConnectionInfo` / `#4 MigrationPlan` → `#5 EF Migration` → `#6 Engine factories` → `#7 DI refactor`

Independent: `#8 Exception middleware`, `#9 Request validation`, `#10 Health check`, `#11 Docs`

## Known Pitfalls

- **`Progress<T>` race condition**: Use `SynchronousProgress<T>` in tests (no SyncContext in test runner)
- **EF Core auto-migration guard**: `db.Database.Migrate()` guarded with `IsRelational()` for in-memory test safety
- **SignalR method names**: Frontend calls `JoinMigration` — must match exactly (silent failure on mismatch)
- **MigrationPlan lifecycle**: `Pending` → `Scheduled` → `Running` → `Completed`/`Failed`; immediate: `Pending` → `Running` → done
- **Nginx WebSocket proxy**: Requires `proxy_http_version 1.1`, `Upgrade`, and `Connection: upgrade` headers
- **ESLint**: `react-hooks/set-state-in-effect` prohibits synchronous setState in useEffect; use async or useMemo
- **Fast Refresh**: Files must export only React components; context and hooks in separate files
