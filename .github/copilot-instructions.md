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
