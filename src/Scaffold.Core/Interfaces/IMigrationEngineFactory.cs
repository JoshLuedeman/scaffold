using Scaffold.Core.Enums;

namespace Scaffold.Core.Interfaces;

public interface IMigrationEngineFactory
{
    IMigrationEngine Create(DatabasePlatform platform);

    /// <summary>
    /// Creates a migration engine for the given source and target platform combination.
    /// Use this overload for cross-platform migrations (e.g., SQL Server → PostgreSQL).
    /// Default implementation falls back to source-only Create for backward compatibility.
    /// </summary>
    IMigrationEngine Create(DatabasePlatform source, DatabasePlatform target) => Create(source);

    IEnumerable<DatabasePlatform> SupportedPlatforms { get; }
}