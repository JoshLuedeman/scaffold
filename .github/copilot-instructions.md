# Copilot Instructions — Scaffold

## Build & Test

```bash
# Backend (.NET 8)
dotnet build                                        # Build entire solution
dotnet test                                         # Run all 211+ tests
dotnet test --filter "FullyQualifiedName~TestName"  # Run a single test
dotnet run --project src/Scaffold.Api               # Run API on localhost:5062

# Frontend (React + TypeScript)
cd src/Scaffold.Web
npm install                                         # Install frontend deps
npm run dev                                         # Vite dev server on localhost:5173
npx tsc --noEmit                                    # TypeScript type check

# Docker (full stack)
docker compose up --build                           # SQL Server + API + Web
docker compose down                                 # Stop all containers

# Azure deployment
azd up                                              # Deploy to Azure (interactive)
```

## Architecture

.NET 8 solution with a React frontend and pluggable engine architecture for assessing and migrating SQL Server databases to Azure SQL services.

### Backend Projects

- **Scaffold.Core** — Domain models, interfaces, enums. Zero project dependencies. All other projects reference this.
- **Scaffold.Assessment** — Implements `IAssessmentEngine` per source platform (e.g., `SqlServer/SqlServerAssessor.cs`). Contains `AzurePricingService`, `TierRecommender`, `MigrationScriptGenerator`, and compatibility checks.
- **Scaffold.Migration** — Implements `IMigrationEngine` per source platform (e.g., `SqlServer/SqlServerMigrator.cs`). Contains `SchemaDeployer` (DACPAC), `BulkDataCopier` (SqlBulkCopy), `ScriptExecutor`, and `ValidationEngine`.
- **Scaffold.Infrastructure** — EF Core `ScaffoldDbContext`, repository implementations, Azure SDK wrappers. References only Core.
- **Scaffold.Api** — ASP.NET Core Web API. Controllers, DTOs, SignalR hub (`MigrationHub`), services (`MigrationProgressService`, `MigrationSchedulerService`). References all projects.

### Frontend

- **Scaffold.Web** — React 19 + TypeScript 5.9, Vite, Fluent UI v9. MSAL for Azure AD auth. SignalR client for real-time migration progress. Proxies `/api` and `/hubs` to the API in dev mode.

### Test Projects

- **Scaffold.Assessment.Tests** — xUnit + Moq. Tests for assessors, pricing, compatibility, tier recommender, script generator.
- **Scaffold.Api.Tests** — xUnit + `WebApplicationFactory` + in-memory EF Core. Integration tests for controllers, auth, scheduler, progress persistence.
- **Scaffold.Migration.Tests** — xUnit + Moq. Tests for migrator, schema deployer, data copier, script executor.

## Key Patterns

### Data Flow
1. User creates a project with a source SQL Server connection
2. Assessment engine analyzes schema, data profile, performance, compatibility
3. `TierRecommender` suggests Azure SQL target tier with pricing
4. User configures migration plan (strategy, scripts, target connection)
5. Plan is approved → executed immediately or scheduled via `MigrationSchedulerService`
6. Migration: schema deploy (DACPAC) → pre-scripts → data copy (SqlBulkCopy) → post-scripts → validation

### Database (EF Core)
- `ScaffoldDbContext` with 6 DbSets: `MigrationProjects`, `ConnectionInfos`, `AssessmentReports`, `MigrationPlans`, `MigrationResults`, `MigrationProgressRecords`
- Complex types stored as JSON columns (`ToJson()`): Schema, DataProfile, Performance, CompatibilityIssues, Recommendation, Scripts
- Enums stored as strings
- Auto-migration on API startup (guarded with `IsRelational()` for test safety)

### Authentication
- **Production**: Microsoft Entra ID (Azure AD) via `Microsoft.Identity.Web`
- **Development**: `DevAuthHandler` auto-authenticates when `DisableAuth=true`
- **Frontend**: MSAL (`@azure/msal-react`) for SPA auth flow

### Real-time Updates
- `MigrationHub` (SignalR) broadcasts migration progress to clients grouped by migration ID
- `MigrationProgressService` sends SignalR events AND persists progress to `MigrationProgressRecords` table

### Background Services
- `MigrationSchedulerService` — Polls every 30s for approved plans where `ScheduledAt <= now` and `Status == Scheduled`

## Conventions

- Each source database platform gets its own subfolder in Assessment and Migration (e.g., `SqlServer/`). New platforms are added by implementing `IAssessmentEngine` and `IMigrationEngine`.
- Domain models in `Scaffold.Core/Models/`, interfaces in `Scaffold.Core/Interfaces/`, enums in `Scaffold.Core/Enums/`.
- API DTOs live in `Scaffold.Api/Dtos/` with `FromModel()` static factory methods.
- Credentials use Azure Key Vault in production (`ConnectionInfo.KeyVaultSecretUri`). `ConnectionInfo.Password` is available for dev/local use via `BuildConnectionString()`.
- Controllers use `[Authorize]` — all endpoints require auth.
- Tests use `CustomWebApplicationFactory` with in-memory DB and `TestAuthHandler` for auto-auth. `StubMigrationEngine` replaces the real engine.
- Pre/post migration scripts are structured as `MigrationScript` objects (canned or custom) with SQL generated from the assessment schema.

## Docker Compose (local dev)

- **db** — SQL Server 2025 on port 1433 (SA password: `SQLSERVER2025!`), with health check
- **api** — .NET 8 API on port 8080, connects to `db`, auth disabled
- **web** — Nginx on port 3000, proxies `/api` and `/hubs` to API (includes WebSocket headers)

## Azure Deployment (azd)

- `azure.yaml` defines api (Container App) and web (Static Web App)
- `hooks/preprovision.sh` — Creates Entra ID app registration if needed
- `hooks/postprovision.sh` — Updates redirect URIs with deployed URLs
- `infra/` — Bicep modules: SQL Database, Container App, Static Web App, ACR, Key Vault, Storage
- Bicep uses conditional auth: if `azureClientId` is provided, configures Entra ID env vars on the Container App; otherwise sets `DisableAuth=true`

## Known Pitfalls & Gotchas

### Progress<T> Race Condition
`System.Progress<T>` posts callbacks asynchronously via `SynchronizationContext`. In test environments (no UI thread / no SyncContext), assertions can run before callbacks complete. Use the `SynchronousProgress<T>` helper in `SqlServerMigratorTests.cs` instead of `Progress<T>` when testing code that reports progress.

### EF Core Auto-Migration Guard
`db.Database.Migrate()` in `Program.cs` is guarded with `db.Database.IsRelational()`. Without this guard, API integration tests using `UseInMemoryDatabase()` will crash because in-memory providers don't support migrations.

### Dependency Direction — No Circular References
`Scaffold.Migration` must NOT reference `Scaffold.Assessment` (and vice versa). Both reference only `Scaffold.Core`. When migration needs assessment data (e.g., canned script SQL), the `MigrationController` pre-populates it by calling `MigrationScriptGenerator.Generate()` at the API layer before passing the plan to the migration engine.

### ScriptExecutor Isolation
`ScriptExecutor` runs raw SQL against the target database with a 300-second timeout per script. It deliberately swallows no exceptions — failures bubble up and mark the migration as failed. Scripts with empty `SqlContent` are skipped.

### SignalR Hub Method Names
The frontend client calls `connection.invoke('JoinMigration', migrationId)`. The hub method must match this name exactly (`JoinMigration`, not `JoinMigrationGroup` or similar). Method name mismatches fail silently — the client won't receive progress updates.

### MigrationPlan Status Lifecycle
`Pending` → `Scheduled` (when approved with a `ScheduledAt` date) → `Running` → `Completed` or `Failed`. Immediate migrations go `Pending` → `Running` → `Completed`/`Failed`. The `MigrationSchedulerService` only picks up plans with `Status == Scheduled`.

### MigrationScript Model (Canned vs Custom)
Canned scripts have a `ScriptId` (e.g., `drop-foreign-keys`) and their `SqlContent` is generated at execution time from the assessment schema via `MigrationScriptGenerator`. Custom scripts store user-provided SQL directly. Both are stored as owned JSON collections on `MigrationPlan`.

### Nginx WebSocket Proxy
The `nginx.conf` in the Web Dockerfile must include `proxy_http_version 1.1`, `Upgrade`, and `Connection: upgrade` headers for the `/hubs/` location block. Without these, SignalR WebSocket connections will fail and fall back to long-polling.

### Frontend API Proxy (Dev)
Vite dev server proxies `/api` and `/hubs` to `http://localhost:5062` (the API). This is configured in `vite.config.ts`. The frontend never hardcodes API URLs — all requests go to relative paths.

### UI Fallbacks for Missing Data
Assessment recommendations may have empty `ServiceTier` or `ComputeSize` (e.g., when pricing data is unavailable). The UI uses `|| 'Not Available'` / `|| 'N/A'` fallbacks to avoid showing "None None".

## Roadmap / Future Work

- **Azure Data Factory integration** — Alternative to SqlBulkCopy for large-scale data movement
- **PostgreSQL support** — New platform engine implementing `IAssessmentEngine` and `IMigrationEngine`
- **Migration progress crash recovery** — Resume interrupted migrations using persisted `MigrationProgressRecords`
- **E2E browser tests** — Automated Playwright tests for the full create → assess → plan → migrate flow
