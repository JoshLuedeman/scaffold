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
dotnet test                                         # Run all 394+ tests
dotnet test --filter "FullyQualifiedName~TestName"  # Single test
dotnet run --project src/Scaffold.Api               # API on localhost:5062
cd src/Scaffold.Web && npm install && npm run dev   # Frontend on localhost:5173
npx tsc --noEmit                                    # TypeScript check
docker compose up --build                           # Full stack locally
azd up                                              # Deploy to Azure
```

## Current State & Roadmap

### Milestones

| Phase | Focus | Status |
|---|---|---|
| **Phase 0**: Foundation & Production Readiness | Multi-platform refactoring, error handling, health checks | ✅ Complete (17 issues) |
| **Phase 0.5**: Testing & Quality | Comprehensive test coverage, frontend tests, CI improvements | 🔜 Next |
| **Phase 1**: PostgreSQL Assessment Engine | Npgsql, PG schema/data/perf analysis, compatibility, pricing | Planned |
| **Phase 2**: SQL Server → PostgreSQL Migration | Data type mapping, DDL translation, schema deploy, bulk data | Planned |
| **Phase 3**: PostgreSQL → Azure PG Migration | PG schema extractor, data copier, logical replication | Planned |
| **Phase 4**: UI & API Multi-Platform | Controllers, TypeScript types, platform selector, PG progress | Planned |
| **Phase 5**: Documentation & Release Prep | ADR, README, API docs, Docker Compose PG, MEMORY.md | Planned |

### Phase 0 Lessons Learned
- **Parallel coder agents**: Must provide explicit branch guidance (e.g., "commit to milestone/phase-0-foundation"). Without it, agents default to creating feature branches and PRs, causing git state conflicts in a shared working directory. Monitor agent output early to catch rogue branching.
- **Coder agent file updated** (`.github/agents/coder.agent.md`): Now supports milestone branch workflow and asks for branch guidance if not provided.
- **Record DataAnnotations**: In ASP.NET Core 8 records, do NOT use `[property: Required]` target syntax — use `[Required]` directly. The `property:` target causes `InvalidOperationException` at runtime.
- **SynchronousProgress<T>**: Required in tests because `Progress<T>` posts callbacks via SynchronizationContext which doesn't exist in xUnit test runners, causing race conditions.
- **EF Core in-memory provider**: Does not support `IsRowVersion()` — concurrency tests need special handling.

### Phase 0 Architecture Additions
- **Engine Factory Pattern**: `IAssessmentEngineFactory` / `IMigrationEngineFactory` resolve platform-specific engines. Controllers no longer inject engines directly.
- **AuditableEntity**: Base class with `CreatedAt`/`UpdatedAt`; `SaveChangesAsync` override auto-sets timestamps.
- **Optimistic Concurrency**: `RowVersion` on `MigrationProject` and `MigrationPlan` via `[Timestamp]` attribute.
- **Connection String Encryption**: `IConnectionStringProtector` using ASP.NET Core Data Protection; encrypts `SourceConnectionString` and `ExistingTargetConnectionString` at rest.
- **Pre-Migration Validation**: `IPreMigrationValidator` validates plans before execution (strategy, schedule, objects, connection strings).
- **API Pagination**: `PaginatedResult<T>` with page/pageSize clamping (1-100).

## Known Pitfalls

- **`Progress<T>` race condition**: Use `SynchronousProgress<T>` in tests (no SyncContext in test runner)
- **EF Core auto-migration guard**: `db.Database.Migrate()` guarded with `IsRelational()` for in-memory test safety
- **SignalR method names**: Frontend calls `JoinMigration` — must match exactly (silent failure on mismatch)
- **MigrationPlan lifecycle**: `Pending` → `Scheduled` → `Running` → `Completed`/`Failed`; immediate: `Pending` → `Running` → done
- **Nginx WebSocket proxy**: Requires `proxy_http_version 1.1`, `Upgrade`, and `Connection: upgrade` headers
- **ESLint**: `react-hooks/set-state-in-effect` prohibits synchronous setState in useEffect; use async or useMemo
- **Fast Refresh**: Files must export only React components; context and hooks in separate files
