using Scaffold.Core.Models;

namespace Scaffold.Core.Interfaces;

public interface IAssessmentEngine
{
    string SourcePlatform { get; }
    Task<bool> TestConnectionAsync(ConnectionInfo source);
    Task<AssessmentReport> AssessAsync(ConnectionInfo source, CancellationToken ct = default);
    Task<TierRecommendation> RecommendTierAsync(AssessmentReport report);
}
