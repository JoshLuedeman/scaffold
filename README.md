# Scaffold

A web application for assessing, planning, and executing SQL Server database migrations to Azure SQL services.

![CI](https://github.com/JoshLuedeman/scaffold/actions/workflows/ci.yml/badge.svg)

## Features

- **Multi-Platform Support** — Extensible platform architecture with SQL Server support today and PostgreSQL planned; platform-specific engines are resolved at runtime via factory pattern
- **Assessment Engine** — Analyzes source databases for schema, data profile, and workload metrics
- **Compatibility Matrix** — Evaluates compatibility across Azure SQL Database, Hyperscale, Managed Instance, and SQL Server on Azure VM
- **Tier Recommendation** — Recommends the optimal Azure SQL service tier based on workload and compatibility
- **Azure Pricing Integration** — Real-time cost estimates from the Azure Retail Prices API across all regions
- **Pre-Migration Validation** — Validates migration plans before execution (strategy, schedule, objects, connection strings, scripts)
- **Migration Scripts** — Auto-generates pre/post migration scripts (drop/restore FKs, indexes, triggers, constraints) with custom script support
- **Bulk Data Migration** — SqlBulkCopy-based data transfer with FK dependency ordering and progress tracking
- **Continuous Sync** — Change Tracking-based incremental replication with manual cutover for minimal downtime
- **Post-Migration Validation** — Row count and checksum verification between source and target
- **Real-Time Progress** — SignalR-based live migration progress updates
- **API Pagination** — Paginated list endpoints with total count, page metadata, and navigation flags
- **Health Check** — `/healthz` endpoint with database connectivity verification
- **Connection String Encryption** — Connection strings encrypted at rest using ASP.NET Core Data Protection
- **Audit Timestamps** — Automatic `CreatedAt`/`UpdatedAt` tracking on all core entities
- **Optimistic Concurrency** — `RowVersion`-based concurrency protection on projects and migration plans

## Architecture

```
┌─────────────────┐       ┌─────────────────────────────────────────────┐
│  Scaffold.Web   │       │              Scaffold.Api                   │
│  React / Vite   │──────▶│          ASP.NET Core 8 API                │
│  Fluent UI v9   │  HTTP │                                             │
└─────────────────┘  + WS │  ┌───────────────────┐ ┌─────────────────┐ │
                          │  │Scaffold.Assessment│ │Scaffold.Migration│ │
                          │  │  Assessment engines│ │ Migration engines│ │
                          │  └────────┬──────────┘ └────────┬────────┘ │
                          │           │                      │          │
                          │  ┌────────▼──────────────────────▼────────┐ │
                          │  │       Scaffold.Infrastructure          │ │
                          │  │   EF Core · Repositories · Azure SDK   │ │
                          │  └────────────────┬───────────────────────┘ │
                          └───────────────────┼─────────────────────────┘
                                              │
                                    ┌─────────▼─────────┐
                                    │    SQL Server      │
                                    │  (local / Azure)   │
                                    └───────────────────┘
```

**Projects:**

- `Scaffold.Core` — Domain models, interfaces, enums. Zero dependencies.
- `Scaffold.Assessment` — Assessment engines per source platform (SQL Server assessor, compatibility checker, tier recommender, pricing service)
- `Scaffold.Migration` — Migration engines (schema deployer via DACPAC, bulk data copier, change tracking sync, script executor, validation)
- `Scaffold.Infrastructure` — EF Core data access, repositories, Azure SDK integrations
- `Scaffold.Api` — ASP.NET Core 8 Web API with SignalR hubs
- `Scaffold.Web` — React 19 + Vite + Fluent UI v9 frontend

Source platform support is extensible via the factory pattern — `IAssessmentEngineFactory` and `IMigrationEngineFactory` resolve platform-specific engines at runtime based on the `DatabasePlatform` enum. See [Architecture](docs/architecture.md) for details.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (for SQL Server and containerized deployment)

## Quick Start (Docker)

The fastest way to run Scaffold:

```bash
docker compose up --build
```

This starts:
- **SQL Server 2025** on port 1433
- **API** on port 8080
- **Web UI** on port 3000

Open http://localhost:3000 in your browser.

## Development Setup

### 1. Start SQL Server

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 --name sqlserver \
  -d mcr.microsoft.com/mssql/server:2025-latest
```

Update the connection string in `src/Scaffold.Api/appsettings.Development.json` if you use a different password.

### 2. Run the API

```bash
cd src/Scaffold.Api
dotnet run
```

The API starts on http://localhost:5062 with authentication disabled for local development. The database is created automatically on first run via EF Core migrations.

### 3. Run the Frontend

```bash
cd src/Scaffold.Web
npm install
npm run dev
```

The frontend starts on http://localhost:5173 and proxies API requests to the backend automatically.

### 4. Run Tests

```bash
dotnet test                                         # All 211+ backend tests
cd src/Scaffold.Web && npx tsc --noEmit             # Frontend type check
```

> **Note:** Backend tests use an in-memory database — no SQL Server instance is required to run tests.

## Workflow

1. **Create a Project** — Name your migration project
2. **Assess** — Connect to a source SQL Server and run a compatibility assessment
3. **Review** — View compatibility scores per Azure service, tier recommendations, and pricing estimates
4. **Plan** — Select objects to migrate, configure target database, choose pre/post migration scripts
5. **Execute** — Run the migration with real-time progress tracking and post-migration validation

## Deploy to Azure

### Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) — logged in (`az login`)
- [Azure Developer CLI (azd)](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) — logged in (`azd auth login`)
- An Azure subscription with permissions to create resources (Contributor role or higher)
- Permissions to create Entra ID App Registrations (or an existing one to provide)

### Step-by-Step Deployment

#### 1. Start the deployment

```bash
azd up
```

#### 2. Select your environment

You'll be prompted to name your environment (e.g., `dev`, `staging`, `prod`). This name prefixes all Azure resources.

```
? Enter a new environment name: dev
```

#### 3. Choose your Azure subscription and region

Select the subscription and Azure region for deployment. Choose a region close to your users and source databases.

```
? Select an Azure Subscription to use: My Subscription (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
? Select an Azure location to use:     East US 2
```

#### 4. Configure authentication

The deployment will ask if you have an existing Entra ID App Registration:

**Option A — Create a new one (recommended for first-time setup):**
```
Do you have an existing Azure AD App Registration? (y/N): N
Creating new App Registration: scaffold-dev...
✓ App Registration created: <CLIENT_ID>
✓ Service Principal created
✓ SPA platform configured
✓ Microsoft Graph User.Read permission added

⚠ IMPORTANT: After deployment completes, an admin must grant API permissions:
  az ad app permission admin-consent --id <CLIENT_ID>
```

**Option B — Use an existing one:**
```
Do you have an existing Azure AD App Registration? (y/N): y
Enter the Application (Client) ID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
Enter the Tenant ID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

#### 5. Wait for provisioning

Bicep deploys all infrastructure (~5-10 minutes):
- Azure SQL Database (Basic tier)
- Azure Container App (API)
- Azure Static Web App (frontend)
- Azure Container Registry (container images)
- Azure Key Vault (secrets)
- Azure Storage Account (migration artifacts)

#### 6. Wait for build and deploy

The CLI builds the API Docker container and frontend, then deploys both (~3-5 minutes).

#### 7. Post-deployment

The deployment automatically updates the App Registration with the correct redirect URIs. A summary is displayed:

```
═══════════════════════════════════════════
  Scaffold Deployment Summary
═══════════════════════════════════════════
  Frontend URL: https://your-app.azurestaticapps.net
  API URL:      https://your-api.azurecontainerapps.io
  Client ID:    xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
═══════════════════════════════════════════
```

#### 8. Grant admin consent (new App Registration only)

If a new App Registration was created, run:

```bash
az ad app permission admin-consent --id <CLIENT_ID>
```

This grants the app permission to read user profiles. Without this step, users cannot sign in.

### Updating an Existing Deployment

To update code without re-provisioning infrastructure:

```bash
azd deploy
```

To update infrastructure and code:

```bash
azd up
```

### Tearing Down

To remove all Azure resources:

```bash
azd down --purge
```

### What Gets Deployed

| Resource | Purpose |
|----------|---------|
| Azure SQL Database | Scaffold metadata store |
| Azure Container App | API backend (.NET 8) |
| Azure Static Web App | Frontend (React) |
| Azure Container Registry | API container image hosting |
| Azure Key Vault | Secret storage for database credentials |
| Azure Storage Account | Migration artifacts |
| Entra ID App Registration | User authentication (created or provided) |

## Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | Scaffold metadata database | `Server=localhost;Database=ScaffoldDb;...` |
| `AzureAd__TenantId` | Entra ID tenant | Set by deployment |
| `AzureAd__ClientId` | App Registration client ID | Set by deployment |
| `DisableAuth` | Disable authentication (dev only) | `false` |

> ⚠️ **Warning**: Never set `DisableAuth=true` in production.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.

## Troubleshooting

### Docker Compose: API fails to start
The API waits for SQL Server to be healthy before starting. If the health check fails, verify the SA password matches between the `db` and `api` services in `docker-compose.yml`.

### Tests fail with "Failed to determine the https port for redirect"
This is a harmless ASP.NET Core warning from the test host. It does not indicate a test failure — check the actual test results.

### Frontend shows blank page / API calls fail (dev mode)
Ensure the API is running on port 5062 before starting the frontend. Vite proxies `/api` requests to `http://localhost:5062`.

### EF Core migration errors on startup
The API auto-applies EF Core migrations on startup. If you see migration errors, ensure your SQL Server is running and the connection string in `appsettings.Development.json` is correct. You can also apply migrations manually:
```bash
dotnet ef database update --project src/Scaffold.Infrastructure --startup-project src/Scaffold.Api
```

### SignalR migration progress not updating
Verify the API is reachable and the browser console doesn't show WebSocket connection errors. In Docker, ensure the nginx config includes WebSocket upgrade headers for the `/hubs/` path.

### Azure deployment: "az ad app permission admin-consent" fails
You need Azure AD admin permissions (Global Administrator or Privileged Role Administrator) to grant consent. Contact your Azure AD admin or have them run the command.

## License

See [LICENSE](LICENSE) for details.
