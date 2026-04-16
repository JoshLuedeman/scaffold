using Scaffold.Core.Enums;

namespace Scaffold.Core.Interfaces;

public interface IAssessmentEngineFactory
{
    IAssessmentEngine Create(DatabasePlatform platform);
    IEnumerable<DatabasePlatform> SupportedPlatforms { get; }
}
