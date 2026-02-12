# Scaffold

A web application for assessing, planning, and executing SQL Server database migrations to Azure SQL services.

![CI](https://github.com/YOUR_ORG/scaffold/actions/workflows/ci.yml/badge.svg)

## Features

- **Assessment Engine** — Analyzes source SQL Server databases for schema, data profile, and workload metrics
- **Compatibility Matrix** — Evaluates compatibility across Azure SQL Database, Hyperscale, Managed Instance, and SQL Server on Azure VM
- **Tier Recommendation** — Recommends the optimal Azure SQL service tier based on workload and compatibility
- **Azure Pricing Integration** — Real-time cost estimates from the Azure Retail Prices API across all regions
- **Migration Scripts** — Auto-generates pre/post migration scripts (drop/restore FKs, indexes, triggers, constraints) with custom script support
- **Bulk Data Migration** — SqlBulkCopy-based data transfer with FK dependency ordering and progress tracking
- **Continuous Sync** — Change Tracking-based incremental replication with manual cutover for minimal downtime
- **Post-Migration Validation** — Row count and checksum verification between source and target
- **Real-Time Progress** — SignalR-based live migration progress updates

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

Source platform support is extensible via `IAssessmentEngine` and `IMigrationEngine` interfaces.

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

### 2. Run the API

```bash
cd src/Scaffold.Api
dotnet run
```

The API starts on http://localhost:5062 with authentication disabled for local development.

### 3. Run the Frontend

```bash
cd src/Scaffold.Web
npm install
npm run dev
```

The frontend starts on http://localhost:5173 and proxies API requests to the backend.

### 4. Run Tests

```bash
dotnet test
```

## Workflow

1. **Create a Project** — Name your migration project
2. **Assess** — Connect to a source SQL Server and run a compatibility assessment
3. **Review** — View compatibility scores per Azure service, tier recommendations, and pricing estimates
4. **Plan** — Select objects to migrate, configure target database, choose pre/post migration scripts
5. **Execute** — Run the migration with real-time progress tracking and post-migration validation

## Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | Scaffold metadata database | `Server=localhost;Database=ScaffoldDb;...` |
| `DisableAuth` | Disable Azure AD authentication (dev only) | `false` |

> ⚠️ **Warning**: Never set `DisableAuth=true` in production.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.

## License

See [LICENSE](LICENSE) for details.
