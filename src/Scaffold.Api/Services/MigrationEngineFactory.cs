using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Migration.PostgreSql;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Api.Services;

public class MigrationEngineFactory : IMigrationEngineFactory
{
    private readonly Dictionary<DatabasePlatform, Func<IMigrationEngine>> _factories;
    private readonly Dictionary<(DatabasePlatform Source, DatabasePlatform Target), Func<IMigrationEngine>> _crossPlatformFactories;

    public MigrationEngineFactory(IServiceProvider serviceProvider)
    {
        _factories = new()
        {
            [DatabasePlatform.SqlServer] = () => serviceProvider.GetRequiredService<SqlServerMigrator>()
        };

        _crossPlatformFactories = new()
        {
            [(DatabasePlatform.SqlServer, DatabasePlatform.PostgreSql)] =
                () => serviceProvider.GetRequiredService<SqlServerToPostgreSqlMigrator>()
        };
    }

    public IMigrationEngine Create(DatabasePlatform platform)
    {
        if (_factories.TryGetValue(platform, out var factory))
            return factory();

        throw new NotSupportedException($"No migration engine registered for platform: {platform}");
    }

    /// <summary>
    /// Creates a migration engine for the given source → target combination.
    /// Falls back to same-platform engine when source == target.
    /// </summary>
    public IMigrationEngine Create(DatabasePlatform source, DatabasePlatform target)
    {
        // Same-platform: delegate to existing single-platform factory
        if (source == target)
            return Create(source);

        // Cross-platform: look up by (source, target) tuple
        if (_crossPlatformFactories.TryGetValue((source, target), out var factory))
            return factory();

        throw new NotSupportedException(
            $"No migration engine registered for cross-platform migration: {source} → {target}");
    }

    public IEnumerable<DatabasePlatform> SupportedPlatforms => _factories.Keys;
}