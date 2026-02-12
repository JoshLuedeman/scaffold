# Contributing to Scaffold

Thanks for your interest in contributing to Scaffold! This guide will help you get started.

## Development Setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/)

### Getting Started

1. Fork and clone the repository
2. Start a SQL Server instance:
   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
     -p 1433:1433 -d mcr.microsoft.com/mssql/server:2025-latest
   ```
3. Run the API:
   ```bash
   dotnet run --project src/Scaffold.Api
   ```
4. Run the frontend:
   ```bash
   cd src/Scaffold.Web && npm install && npm run dev
   ```

## Running Tests

```bash
# All tests
dotnet test

# Specific test project
dotnet test tests/Scaffold.Assessment.Tests

# Frontend type checking
cd src/Scaffold.Web && npx tsc --noEmit
```

## Project Structure

```
src/
  Scaffold.Core/          # Domain models, interfaces, enums (no dependencies)
  Scaffold.Assessment/    # Assessment engines (SQL Server assessor, compatibility, pricing)
  Scaffold.Migration/     # Migration engines (schema deploy, bulk copy, sync, validation)
  Scaffold.Infrastructure/# EF Core, repositories, Azure SDK
  Scaffold.Api/           # ASP.NET Core 8 Web API
  Scaffold.Web/           # React 19 + Vite + Fluent UI v9
tests/
  Scaffold.Assessment.Tests/
  Scaffold.Api.Tests/
  Scaffold.Migration.Tests/
```

## Code Style

- Follow existing patterns and conventions in the codebase
- Use meaningful variable and method names
- Keep methods focused and small
- Add XML doc comments on public APIs
- Frontend uses TypeScript strict mode

## Pull Request Guidelines

1. **Create a feature branch** from `main`
2. **Write tests** for new functionality
3. **Run all tests** before submitting: `dotnet test`
4. **Type-check the frontend**: `cd src/Scaffold.Web && npx tsc --noEmit`
5. **Keep PRs focused** — one feature or fix per PR
6. **Write clear commit messages** in imperative mood (e.g., "Add feature" not "Added feature")

## Adding a New Database Platform

Scaffold supports a pluggable engine architecture. To add support for a new source database:

1. Create a subfolder in `src/Scaffold.Assessment/` (e.g., `PostgreSql/`)
2. Implement `IAssessmentEngine` for schema analysis and compatibility checking
3. Create a subfolder in `src/Scaffold.Migration/` (e.g., `PostgreSql/`)
4. Implement `IMigrationEngine` for data migration
5. Register the engines in `src/Scaffold.Api/Program.cs`

## Reporting Issues

- Use GitHub Issues for bug reports and feature requests
- Include steps to reproduce for bugs
- Include expected vs actual behavior

## Security

If you discover a security vulnerability, please see [SECURITY.md](SECURITY.md) for responsible disclosure instructions.
