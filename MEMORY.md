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
| `Scaffold.Integration.Tests` | xUnit + real SQL Server + PostgreSQL | End-to-end against live databases (CI with service containers) |

## Key Technical Decisions

- **EF Core JSON columns**: Complex types (Schema, DataProfile, Performance, CompatibilityIssues, Recommendation, Scripts) stored via `ToJson()`
- **Enums stored as strings** in the database
- **No circular references**: Assessment and Migration must NOT reference each other; both reference only Core
- **Auth**: Microsoft Entra ID (production) / `DevAuthHandler` with `DisableAuth=true` (development)
- **Real-time**: SignalR `MigrationHub` broadcasts progress and persists to `MigrationProgressRecords`
- **Background scheduler**: `MigrationSchedulerService` polls every 30s for approved plans
- **Docker Compose**: SQL Server 2025 + PostgreSQL 16 + API + Nginx (runtime DNS resolution with `resolver 127.0.0.11`)

## Build & Test Commands

```bash
dotnet build                                        # Build entire solution
dotnet test                                         # Run all 611+ tests
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
| **Phase 0**: Foundation & Production Readiness | Multi-platform refactoring, error handling, health checks | ✅ Merged (PR #89, 17 issues) |
| **Phase 0.5**: Infrastructure & DevOps Hardening | Observability, CD pipeline, security scanning, VNet, alerts | ✅ Merged (PR #90, 10 issues) |
| **Phase 1**: PostgreSQL Assessment Engine | Npgsql, PG schema/data/perf analysis, compatibility, pricing | ✅ Merged (PR #119, 15 issues) |
| **Phase 2**: SQL Server → PostgreSQL Migration | Data type mapping, DDL translation, schema deploy, bulk data | Planned |
| **Phase 3**: PostgreSQL → Azure PG Migration | PG schema extractor, data copier, logical replication | Planned |
| **Phase 4**: UI & API Multi-Platform | Controllers, TypeScript types, platform selector, PG progress | Planned |
| **Phase 5**: Documentation & Release Prep | ADR, README, API docs, Docker Compose PG, MEMORY.md | Planned |

### Phase 0 Lessons Learned
- **Parallel coder agents**: Must provide explicit branch guidance (e.g., "commit to milestone/phase-0-foundation"). Without it, agents default to creating feature branches and PRs, causing git state conflicts in a shared working directory. Monitor agent output early to catch rogue branching.
- **Coder agent file updated** (`.github/agents/coder.agent.md`): Now supports milestone branch workflow and asks for branch guidance if not provided.
- **Copilot code review**: First Copilot review on a repo requires clicking the UI "Request" button — CLI/API methods (`gh pr edit --add-reviewer`) don't work initially. Subsequent PRs should retry CLI methods.
- **Copilot review found real bugs**: Connection string encryption/decryption flow had a critical bug (encrypted strings passed to engines). Always address suppressed low-confidence comments too — they can be valid.
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

### Phase 0.5 Lessons Learned
- **Copilot review via CLI**: `gh pr edit --add-reviewer "copilot"` still doesn't reliably trigger Copilot reviews — may need UI button click each time.
- **Sequential agent dispatching**: Batching 2 related issues per coder agent (e.g., #58+#59 Bicep, #60+#61 deployment) works well and avoids parallel git conflicts.
- **Conditional Bicep resources**: Use `if` for conditional resource deployment (e.g., VNet only in prod) and `union()` for conditional property merging.

### Phase 0.5 Infrastructure Additions
- **Observability**: Application Insights + Log Analytics workspace connected to Container App managed environment.
- **CD Pipeline**: `cd.yml` workflow with OIDC Azure auth, ACR push, Container App deploy, Static Web App deploy, EF migrations.
- **Security Scanning**: CodeQL (C# + JS/TS), Trivy container scans, Dependabot (NuGet, npm, Actions, Docker), CODEOWNERS.
- **Health Probes**: Startup (5min window), liveness (30s), readiness (checks DB) on Container App.
- **VNet Isolation**: Conditional VNet with private endpoints for SQL + Key Vault (prod only, dev opts out).
- **Azure Monitor Alerts**: 5xx errors, container restarts, migration failures, SignalR failures, high latency.
- **Non-root Docker**: Both API and Web containers run as UID 1000 `appuser`.
- **Production SKUs**: SQL S1, Container App 0.5 CPU/1Gi, Standard ACR/SWA (~$85-105/mo).
- **DB Migration Hook**: `postprovision.sh` runs `dotnet ef database update` during deployment.
- **ESLint CI**: Frontend lint step enforced in CI pipeline.

### Phase 1 Lessons Learned
- **NpgsqlConnection is sealed**: Cannot be mocked with Moq. Unit tests focus on model/mapping logic; DB interaction tested via integration tests with real PostgreSQL.
- **PostgreSQL partitioned table constraints**: Primary keys on partitioned tables must include all partition columns — `PRIMARY KEY (id, viewed_at)` not just `PRIMARY KEY (id)`.
- **Port defaulting pattern**: `PostgreSqlConnectionFactory` checks if port == 1433 (SQL default) and substitutes 5432 for PostgreSQL connections.
- **CI service containers**: PostgreSQL 16 added alongside SQL Server 2022 in the integration test job with health checks (`pg_isready`).
- **6-wave sequential implementation**: Dependency-ordered waves (infra → analyzers → compatibility → pricing → orchestrator → integration tests) worked well for 15 issues.

### Phase 1 Architecture Additions
- **PostgreSQL Assessment Engine**: Full `IAssessmentEngine` implementation (`PostgreSqlAssessor`) with the same component pattern as SQL Server.
- **Components**: `PostgreSqlConnectionFactory`, `SchemaAnalyzer` (9 PG catalog queries), `DataProfiler`, `PerformanceProfiler`, `CompatibilityMatrix` (37 features), `CompatibilityChecker` (9 async checks), `ExtensionDetector` (55 known extensions), `TierRecommender` (Burstable/GP/MO/VM).
- **Azure PG Pricing**: `AzurePricingService` extended with `BuildFlexibleServerFilter()` and `BuildPostgreSqlVmFilter()` for Linux VM pricing.
- **DI Registration**: `PostgreSqlConnectionFactory` (singleton) + `PostgreSqlAssessor` (scoped) registered in `Program.cs`. `AssessmentEngineFactory` maps `DatabasePlatform.PostgreSql` to the new assessor.
- **Integration Tests**: 14 PostgreSQL integration tests with `PostgreSqlFixture` (IAsyncLifetime) seeding from `postgres-seed.sql`.

## Known Pitfalls

- **`Progress<T>` race condition**: Use `SynchronousProgress<T>` in tests (no SyncContext in test runner)
- **EF Core auto-migration guard**: `db.Database.Migrate()` guarded with `IsRelational()` for in-memory test safety
- **SignalR method names**: Frontend calls `JoinMigration` — must match exactly (silent failure on mismatch)
- **MigrationPlan lifecycle**: `Pending` → `Scheduled` → `Running` → `Completed`/`Failed`; immediate: `Pending` → `Running` → done
- **Nginx WebSocket proxy**: Requires `proxy_http_version 1.1`, `Upgrade`, and `Connection: upgrade` headers
- **ESLint**: `react-hooks/set-state-in-effect` prohibits synchronous setState in useEffect; use async or useMemo
- **Fast Refresh**: Files must export only React components; context and hooks in separate files
