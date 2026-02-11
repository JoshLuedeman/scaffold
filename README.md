# Scaffold — Azure Database Migration Tool

A web application for assessing, planning, and executing database migrations to Azure SQL.

## Architecture

- **Scaffold.Api** — ASP.NET Core 8 Web API
- **Scaffold.Core** — Domain models, interfaces, enums (no dependencies)
- **Scaffold.Assessment** — Assessment engines (pluggable per source platform)
- **Scaffold.Migration** — Migration execution engines (cutover + continuous sync)
- **Scaffold.Infrastructure** — Data access, Azure SDK integrations, repositories
- **Scaffold.Web** — React/TypeScript frontend (TBD)

## Prerequisites

- .NET 8 SDK
- Node.js 20+ (for frontend)
- Azure subscription (for deployment)

## Build & Run

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~YourTestName"

# Run the API
dotnet run --project src/Scaffold.Api
```

## Project Structure

Source platform support is extensible via `IAssessmentEngine` and `IMigrationEngine` interfaces. Currently supports SQL Server → Azure SQL.
