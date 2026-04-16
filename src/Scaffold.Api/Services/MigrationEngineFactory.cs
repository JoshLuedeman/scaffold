using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Api.Services;

public class MigrationEngineFactory : IMigrationEngineFactory
{
    private readonly Dictionary<DatabasePlatform, Func<IMigrationEngine>> _factories;

    public MigrationEngineFactory(IServiceProvider serviceProvider)
    {
        _factories = new()
        {
            [DatabasePlatform.SqlServer] = () => serviceProvider.GetRequiredService<SqlServerMigrator>()
        };
    }

    public IMigrationEngine Create(DatabasePlatform platform)
    {
        if (_factories.TryGetValue(platform, out var factory))
            return factory();

        throw new NotSupportedException($"No migration engine registered for platform: {platform}");
    }

    public IEnumerable<DatabasePlatform> SupportedPlatforms => _factories.Keys;
}
