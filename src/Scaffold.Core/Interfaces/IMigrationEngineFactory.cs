using Scaffold.Core.Enums;

namespace Scaffold.Core.Interfaces;

public interface IMigrationEngineFactory
{
    IMigrationEngine Create(DatabasePlatform platform);
    IEnumerable<DatabasePlatform> SupportedPlatforms { get; }
}
