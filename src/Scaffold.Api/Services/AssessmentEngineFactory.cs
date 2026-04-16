using Scaffold.Assessment.PostgreSql;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;

namespace Scaffold.Api.Services;

public class AssessmentEngineFactory : IAssessmentEngineFactory
{
    private readonly Dictionary<DatabasePlatform, Func<IAssessmentEngine>> _factories;

    public AssessmentEngineFactory(IServiceProvider serviceProvider)
    {
        _factories = new()
        {
            [DatabasePlatform.SqlServer] = () => serviceProvider.GetRequiredService<SqlServerAssessor>(),
            [DatabasePlatform.PostgreSql] = () => serviceProvider.GetRequiredService<PostgreSqlAssessor>()
        };
    }

    public IAssessmentEngine Create(DatabasePlatform platform)
    {
        if (_factories.TryGetValue(platform, out var factory))
            return factory();

        throw new NotSupportedException($"No assessment engine registered for platform: {platform}");
    }

    public IEnumerable<DatabasePlatform> SupportedPlatforms => _factories.Keys;
}
