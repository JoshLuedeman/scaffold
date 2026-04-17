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
| **Phase 2**: SQL Server → PostgreSQL Migration | Data type mapping, DDL translation, schema deploy, bulk data | ✅ Merged (PR #120, 8 issues) |
| **Phase 3**: PostgreSQL → Azure PG Migration | PG schema extractor, data copier, logical replication | ✅ Merged (PR #121, 8 issues) |
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

### Phase 2 Lessons Learned
- **Npgsql COPY protocol + transactions**: `BeginBinaryImportAsync` puts the connection in COPY state until the writer is *disposed* (not just after `CompleteAsync`). Must use block-scoped `await using (var writer = ...) { }` to release COPY state before running subsequent SQL commands on the same connection.
- **SQL Server `CHARACTER_MAXIMUM_LENGTH` is Int32**: Using `reader.GetInt64()` throws — use `Convert.ToInt32(reader.GetValue())`.
- **PostgreSQL temporal precision max is 6**: SQL Server `datetime2(7)` has 7 digits; PG max is 6. Clamp with `Math.Clamp(p, 0, 6)`.
- **`GENERATED ALWAYS AS IDENTITY` prevents COPY**: PG rejects explicit identity values via COPY protocol. Use `GENERATED BY DEFAULT AS IDENTITY`.
- **BIT DEFAULT conversion**: PG rejects integer defaults on boolean columns. `DEFAULT 1` → `DEFAULT true`, `DEFAULT 0` → `DEFAULT false`.
- **Identifier escaping patterns**: SQL Server `]` → `]]` inside `[...]`, PostgreSQL `"` → `""` inside `"..."`. Must be applied consistently across all DDL/DML generation.
- **EF Core `IsModified = false`**: Prevents `SaveChangesAsync` from persisting decrypted connection strings back to DB — but must be in the same thread/scope as the DbContext.
- **DbContext is not thread-safe**: Never capture a request-scoped `DbContext` in `Task.Run`. Create a new `IServiceScope` inside the background task.
- **Request `CancellationToken` vs background work**: Don't pass request-scoped `ct` to `Task.Run` for fire-and-forget work — it cancels when the client disconnects.
- **Internal review pipeline**: Run code-review, dba-agent, security-auditor agents on every PR. Route findings through architect for triage. Dispatch coders for "fix now" items. This replaces GitHub Copilot PR reviews.
- **Two-round review pattern**: First review round finds issues → fix → second review round validates fixes and may find new issues in the fixes themselves. Budget for 2 rounds.

### Phase 2 Architecture Additions
- **SQL→PG Migration Engine**: `SqlServerToPostgreSqlMigrator` implementing `IMigrationEngine` for full cutover migrations.
- **Components**: `DataTypeMapper` (50+ type mappings, identity handling, temporal precision clamping), `DdlTranslator` (CREATE TABLE/FK/INDEX with topological sort), `PostgreSqlSchemaDeployer` (schema creation + DDL execution), `CrossPlatformBulkCopier` (COPY protocol with transaction wrapping), `PostgreSqlValidationEngine` (row count + schema validation), `PostgreSqlPrePostScriptHandler` (pre/post migration scripts).
- **Security hardening**: Identifier escaping (`]`→`]]`, `"`→`""`), FK action whitelist, computed column comment sanitization, credential protection via `IsModified=false`, scoped DbContext in background tasks, sanitized error messages via SignalR.
- **MigrationScriptGenerator**: Generates DROP FK/index/trigger and CREATE trigger/view/procedure/function scripts for pre/post migration.
- **Integration tests**: 41 integration tests (35 assessment + 6 cross-platform migration) running against SQL Server 2022 + PostgreSQL 16 in CI.

### Phase 3 Lessons Learned
- **Replication slot ordering is critical**: Must create replication slot *before* initial data load, not after. Otherwise, changes between load completion and slot creation are permanently lost. Use `LogicalSlotSnapshotInitMode.Export` for the slot.
- **`setval()` with double-quoted identifiers fails**: `QuotePgName` produces `"schema"."seq"` which PG parses as a qualified column reference. Use `@seqName::regclass` with parameterized queries instead.
- **Composite FK from `information_schema` produces Cartesian product**: `key_column_usage ⨝ constraint_column_usage` yields N×N rows for N-column composite FKs. Use `pg_constraint` with `unnest(conkey, confkey) WITH ORDINALITY` for correct positional pairing.
- **NULL-safe deletes in replication**: `WHERE col = @param` with `DBNull.Value` evaluates to `UNKNOWN`, silently dropping deletes. Use `IS NOT DISTINCT FROM` instead.
- **Connection-per-message exhausts pool**: Logical replication apply methods must share a single persistent target connection, not open/close per message.
- **`setval()` off-by-one for empty tables**: `setval(seq, 1, true)` makes next `nextval()` return 2. Use `is_called = COALESCE((SELECT MAX(col) FROM t) IS NOT NULL, false)` for correct behavior.
- **UDT types need schema qualification**: `information_schema.columns.udt_name` lacks schema info. Include `udt_schema` and schema-qualify types not in the `public` schema.
- **DDL without transaction**: Partial schema on failure. Always wrap DDL deployment in `BEGIN`/`COMMIT`/`ROLLBACK`.
- **Review pipeline matured**: 3-agent parallel review (code-review + dba-agent + security-auditor) → architect triage → coder fixes → Round 2 review. Budget for 2 rounds.

### Phase 3 Architecture Additions
- **PostgreSQL Schema Extractor**: `PostgreSqlSchemaExtractor` queries `pg_catalog`/`information_schema` for tables, columns, indexes, FKs, sequences, views, functions, extensions. Returns `PgSchemaSnapshot`.
- **DDL Generator**: `PostgreSqlDdlGenerator` generates CREATE statements in dependency order using `TopologicalSorter`. Handles enums, sequences, tables, FKs, indexes, views, functions.
- **Azure Extension Handler**: `AzureExtensionHandler` maps on-prem PG extensions to Azure-compatible alternatives or flags unsupported ones.
- **Bulk Data Copier**: `PostgreSqlBulkCopier` uses COPY protocol for PG→PG streaming with sequence reset and trigger management.
- **Logical Replication**: `LogicalReplicationSyncEngine` implements pgoutput protocol via Npgsql 8.0 native replication for continuous sync with idempotent apply (ON CONFLICT DO NOTHING/UPDATE, IS NOT DISTINCT FROM).
- **Retry Policy**: `ReplicationRetryPolicy` with exponential backoff, circuit breaker, and 13 transient PG error codes.
- **Migration Orchestrator**: `PostgreSqlMigrator` implements 8-step cutover pipeline: assess → extract → generate DDL → deploy schema → extensions → data copy → validate → cutover.
- **Cancellation Service**: `MigrationCancellationService` singleton with `ConcurrentDictionary<Guid, CTS>` for migration cancellation via API endpoint.
- **Validation Engine**: `PostgreSqlToPostgreSqlValidationEngine` for PG→PG row count validation.
- **Test Coverage**: 755 migration tests, 24 integration tests (PG container required).

## Known Pitfalls

- **`Progress<T>` race condition**: Use `SynchronousProgress<T>` in tests (no SyncContext in test runner)
- **EF Core auto-migration guard**: `db.Database.Migrate()` guarded with `IsRelational()` for in-memory test safety
- **SignalR method names**: Frontend calls `JoinMigration` — must match exactly (silent failure on mismatch)
- **MigrationPlan lifecycle**: `Pending` → `Scheduled` → `Running` → `Completed`/`Failed`; immediate: `Pending` → `Running` → done
- **Nginx WebSocket proxy**: Requires `proxy_http_version 1.1`, `Upgrade`, and `Connection: upgrade` headers
- **ESLint**: `react-hooks/set-state-in-effect` prohibits synchronous setState in useEffect; use async or useMemo
- **Fast Refresh**: Files must export only React components; context and hooks in separate files
