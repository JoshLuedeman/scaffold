# Architecture

This document describes Scaffold's system architecture, key design patterns, and cross-cutting concerns. It is intended for contributors and agents working on the codebase.

## System Overview

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
                                    │  Database Server   │
                                    │  (SQL Server now;  │
                                    │  PostgreSQL planned)│
                                    └───────────────────┘
```

**Projects:**

| Project | Responsibility | Dependencies |
|---------|---------------|--------------|
| `Scaffold.Core` | Domain models, interfaces, enums | None (zero dependencies) |
| `Scaffold.Assessment` | Assessment engines per source platform | `Scaffold.Core` |
| `Scaffold.Migration` | Migration engines (schema, data, sync, validation) | `Scaffold.Core` |
| `Scaffold.Infrastructure` | EF Core data access, repositories, security | `Scaffold.Core` |
| `Scaffold.Api` | ASP.NET Core 8 Web API, SignalR hubs, DI composition | All projects |
| `Scaffold.Web` | React 19 + Vite + Fluent UI v9 frontend | API (HTTP + WebSocket) |

## Engine Factory Pattern

Scaffold supports multiple database platforms via the `DatabasePlatform` enum (`SqlServer`, `PostgreSql`). Platform-specific assessment and migration engines are resolved at runtime through the factory pattern rather than direct DI injection.

### How it works

1. **Interfaces** — `IAssessmentEngine` and `IMigrationEngine` define the contract all platform engines must implement.
2. **Factories** — `IAssessmentEngineFactory` and `IMigrationEngineFactory` accept a `DatabasePlatform` value and return the corresponding engine instance.
3. **Registration** — Concrete engines (e.g., `SqlServerAssessor`, `SqlServerMigrator`) are registered in DI. The factory resolves them from `IServiceProvider` using a platform → factory-function dictionary.
4. **Usage** — Controllers and services call `factory.Create(platform)` to get the right engine for a given connection or migration plan.

```
Controller
  │
  ▼
IAssessmentEngineFactory.Create(DatabasePlatform.SqlServer)
  │
  ▼
SqlServerAssessor : IAssessmentEngine
```

**To add a new platform:** Register a new engine class implementing `IAssessmentEngine` and/or `IMigrationEngine`, then add an entry to the factory's dictionary. No existing code changes required.

### Supported platforms

| Platform | Assessment | Migration | Status |
|----------|-----------|-----------|--------|
| SQL Server | `SqlServerAssessor` | `SqlServerMigrator` | ✅ Implemented |
| PostgreSQL | — | — | 🔜 Planned |

## Exception Handling Middleware

All unhandled exceptions are caught by `ExceptionHandlingMiddleware` and returned as [RFC 7807 ProblemDetails](https://datatracker.ietf.org/doc/html/rfc7807) responses. This provides a consistent error format for all API consumers.

**Exception → HTTP status mapping:**

| Exception Type | HTTP Status | Title |
|---------------|-------------|-------|
| `KeyNotFoundException` | 404 | Not Found |
| `ArgumentNullException` | 400 | Bad Request |
| `ArgumentException` | 400 | Bad Request |
| `InvalidOperationException` | 409 | Conflict |
| `UnauthorizedAccessException` | 403 | Forbidden |
| All others | 500 | Internal Server Error |

In Development mode, the `detail` field includes the full exception stack trace. In production, only the title and status are returned.

## Concurrency Protection

`MigrationProject` and `MigrationPlan` use SQL Server's `rowversion` (timestamp) column for optimistic concurrency control.

**How it works:**

1. Each entity has a `byte[] RowVersion` property decorated with `[Timestamp]`.
2. EF Core configures these as `.IsRowVersion()` — SQL Server auto-increments the value on every UPDATE.
3. When a client sends an update, it includes the `RowVersion` it read. If another write happened in between, EF Core throws `DbUpdateConcurrencyException` and the update is rejected.

This prevents lost updates when multiple users or processes modify the same project or plan concurrently, without requiring pessimistic locks.

## Audit Timestamps

All core entities inherit from `AuditableEntity`, which provides automatic `CreatedAt` and `UpdatedAt` timestamps.

```csharp
public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**How timestamps are set:**

- The `ScaffoldDbContext.SaveChangesAsync` override scans the change tracker for `AuditableEntity` entries:
  - **Added** entities → both `CreatedAt` and `UpdatedAt` are set to `DateTime.UtcNow`
  - **Modified** entities → only `UpdatedAt` is updated; `CreatedAt` is explicitly marked as unmodified to prevent accidental changes

**Entities using audit timestamps:** `MigrationProject`, `MigrationPlan`

## Connection String Encryption

Connection strings stored in the database are encrypted at rest using ASP.NET Core Data Protection.

- **Interface:** `IConnectionStringProtector` (in `Scaffold.Core`) — `Protect(string)` / `Unprotect(string)`
- **Implementation:** `ConnectionStringProtector` (in `Scaffold.Infrastructure.Security`) — uses `IDataProtectionProvider` with the purpose string `"Scaffold.ConnectionStrings"`
- **Key management:** Relies on the default ASP.NET Core Data Protection key ring. In Azure deployments, keys are stored in Azure Key Vault.

## Pre-Migration Validation

Before a migration can execute, the `IPreMigrationValidator` service validates the migration plan. Validation checks include:

- Migration strategy is set and valid
- `ScheduledAt` (if provided) is in the future
- At least one object is included in `IncludedObjects`
- `SourceConnectionString` is not empty
- Warnings are issued if pre/post migration scripts are missing

The validator returns a `PreMigrationValidationResult` with separate `Errors` (blocking) and `Warnings` (informational) lists. The `IsValid` property is `true` only when there are zero errors.

## API Pagination

List endpoints support pagination via `PaginatedResult<T>`, which wraps a page of items with navigation metadata:

| Field | Type | Description |
|-------|------|-------------|
| `Items` | `IReadOnlyList<T>` | The items on the current page |
| `TotalCount` | `int` | Total number of items across all pages |
| `Page` | `int` | Current page number (1-based) |
| `PageSize` | `int` | Items per page |
| `TotalPages` | `int` | Total number of pages |
| `HasNextPage` | `bool` | Whether a next page exists |
| `HasPreviousPage` | `bool` | Whether a previous page exists |

## Health Check

The API exposes a health check endpoint at `GET /healthz` that verifies database connectivity via EF Core's `AddDbContextCheck<ScaffoldDbContext>()`. This endpoint is suitable for container orchestrators (Docker, Kubernetes) and load balancers.

---

## Architecture Decision Records

Architecture Decision Records (ADRs) capture the **why** behind significant technical decisions. Each ADR documents the context, the decision made, and its consequences — creating a traceable history of project evolution.

**Why ADRs matter for agents:** Agents encounter decisions made before their session began. Without ADRs, an agent may undo or contradict a prior decision because it lacks context. ADRs give agents the rationale they need to work *with* the project's direction, not against it.

### When to Write an ADR

Write an ADR when a decision:
- Affects multiple components or workflows
- Constrains future technical choices
- Was chosen over a reasonable alternative
- Would confuse a future contributor who wasn't present for the discussion

### ADR Template

```markdown
# ADR-NNN: [Short Title]

**Status:** proposed | accepted | deprecated | superseded by ADR-NNN

**Date:** YYYY-MM-DD

## Context

What is the issue or force motivating this decision? Describe the situation
and constraints. Be specific — agents will rely on this to understand scope.

## Decision

State the decision clearly in one or two sentences, then elaborate if needed.
Use imperative tone: "We will..." or "The project uses..."

## Consequences

What becomes easier or harder as a result of this decision?

- **Positive:** [benefits]
- **Negative:** [trade-offs]
- **Neutral:** [side effects worth noting]

## Alternatives Considered

| Alternative | Why It Was Rejected |
|---|---|
| Option A | Brief reason |
| Option B | Brief reason |
```

### ADR Storage

ADRs are stored as individual files in [`docs/decisions/`](decisions/). See [decisions/README.md](decisions/README.md) for the index.
