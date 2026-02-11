# Copilot Instructions — Scaffold

## Build & Test

```bash
dotnet build                                        # Build entire solution
dotnet test                                         # Run all tests
dotnet test --filter "FullyQualifiedName~TestName"  # Run a single test
dotnet run --project src/Scaffold.Api               # Run the API locally
```

## Architecture

This is a .NET 8 solution with a pluggable engine architecture for database migrations to Azure:

- **Scaffold.Core** — Domain models and interfaces. Has no project dependencies. All other projects reference this.
- **Scaffold.Assessment** — Implements `IAssessmentEngine` per source platform (e.g., `SqlServer/SqlServerAssessor.cs`).
- **Scaffold.Migration** — Implements `IMigrationEngine` per source platform (e.g., `SqlServer/SqlServerMigrator.cs`).
- **Scaffold.Infrastructure** — EF Core data access, Azure SDK wrappers, repository implementations.
- **Scaffold.Api** — ASP.NET Core Web API. References all projects. Contains controllers and DI registration.

## Conventions

- Each source database platform gets its own subfolder in both Assessment and Migration projects (e.g., `SqlServer/`, `PostgreSql/`).
- New platforms are added by implementing `IAssessmentEngine` and `IMigrationEngine` — no changes to Core or Api needed.
- Domain models live in `Scaffold.Core/Models/`, interfaces in `Scaffold.Core/Interfaces/`, enums in `Scaffold.Core/Enums/`.
- Credentials are never stored directly — use Azure Key Vault via `ConnectionInfo.KeyVaultSecretUri`.
- The API uses Entra ID (Azure AD) for user auth; source database connections may use SQL authentication.
