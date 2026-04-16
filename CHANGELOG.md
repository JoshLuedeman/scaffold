# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

### Added

- **Multi-platform database support** — `DatabasePlatform` enum (`SqlServer`, `PostgreSql`) for extensible source platform targeting
- **Platform-aware connections** — `ConnectionInfo.Platform` property with platform-specific connection string building (SQL Server and PostgreSQL formats)
- **Migration plan platform fields** — `MigrationPlan.SourcePlatform` and `MigrationPlan.TargetPlatform` for cross-platform migration planning
- **EF Core migrations** — Database migrations for new platform fields on `ConnectionInfo` and `MigrationPlan`
- **Engine factory pattern** — `IAssessmentEngineFactory` and `IMigrationEngineFactory` interfaces to resolve platform-specific engines at runtime
- **Assessment engine factory** — `AssessmentEngineFactory` resolves `IAssessmentEngine` implementations by `DatabasePlatform`
- **Migration engine factory** — `MigrationEngineFactory` resolves `IMigrationEngine` implementations by `DatabasePlatform`
- **Global exception handling** — `ExceptionHandlingMiddleware` catches unhandled exceptions and returns RFC 7807 ProblemDetails responses
- **Health check endpoint** — `GET /healthz` with EF Core database connectivity verification
- **Pre-migration validation** — `IPreMigrationValidator` service validates migration plans before execution (strategy, schedule, objects, connections, scripts)
- **API pagination** — `PaginatedResult<T>` model with page metadata, total count, and navigation flags
- **Audit timestamps** — `AuditableEntity` base class with automatic `CreatedAt`/`UpdatedAt` tracking via `SaveChangesAsync` override
- **Optimistic concurrency** — `RowVersion` timestamp columns on `MigrationProject` and `MigrationPlan` for conflict detection
- **Connection string encryption** — `IConnectionStringProtector` / `ConnectionStringProtector` using ASP.NET Core Data Protection to encrypt connection strings at rest
- **SignalR hub tests** — Test coverage for real-time migration progress hub
- **ChangeTrackingSyncEngine tests** — Comprehensive test coverage for incremental replication engine
- **Request validation** — DataAnnotations-based validation on API DTOs

### Changed

- **DI refactored to use factories** — Controllers and services now use `IAssessmentEngineFactory` / `IMigrationEngineFactory` instead of direct `IAssessmentEngine` / `IMigrationEngine` injection
- **Architecture documentation** — `docs/architecture.md` expanded from ADR-only reference to full system architecture document

### Fixed

- **ESLint violations** — Resolved all ESLint errors in the frontend codebase
- **Docker nginx DNS resolution** — Fixed nginx upstream DNS resolution for containerized deployments
