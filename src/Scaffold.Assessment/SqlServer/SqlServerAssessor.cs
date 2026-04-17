using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.SqlServer;

public class SqlServerAssessor : Core.Interfaces.IAssessmentEngine
{
    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly ILogger<SqlServerAssessor> _logger;
    private readonly IAzurePricingService? _pricingService;

    public SqlServerAssessor(
        SqlServerConnectionFactory connectionFactory,
        ILogger<SqlServerAssessor> logger,
        IAzurePricingService? pricingService = null)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _pricingService = pricingService;
    }

    public string SourcePlatform => "SqlServer";

    public async Task<bool> TestConnectionAsync(ConnectionInfo source)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(source);
            await using var command = new SqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync();
            _logger.LogInformation("Connection test succeeded for {Server}/{Database}",
                source.Server, source.Database);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for {Server}/{Database}",
                source.Server, source.Database);
            return false;
        }
    }

    public async Task<AssessmentReport> AssessAsync(ConnectionInfo source, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting assessment for {Server}/{Database}", source.Server, source.Database);

        await using var connection = await _connectionFactory.CreateConnectionAsync(source);

        var schemaAnalyzer = new SchemaAnalyzer(connection);
        var schema = await schemaAnalyzer.AnalyzeAsync(ct);

        _logger.LogInformation("Schema analysis complete: {Tables} tables, {Views} views, {Procs} stored procedures",
            schema.TableCount, schema.ViewCount, schema.StoredProcedureCount);

        _logger.LogInformation("Collecting performance metrics for {Server}/{Database}",
            source.Server, source.Database);
        var performance = await PerformanceProfiler.CollectAsync(connection, ct);

        _logger.LogInformation("Collecting data profile for {Server}/{Database}",
            source.Server, source.Database);
        var dataProfile = await DataProfiler.CollectAsync(connection, ct);

        _logger.LogInformation("Checking Azure SQL compatibility for {Server}/{Database}",
            source.Server, source.Database);
        var compatibilityChecker = new CompatibilityChecker(connection);
        var compatibilityIssues = await compatibilityChecker.CheckAsync(ct);

        // Initial score before recommendation (uses raw issues without target-specific severity)
        var compatibilityScore = CompatibilityChecker.CalculateCompatibilityScore(compatibilityIssues);
        var risk = CompatibilityChecker.DetermineRisk(compatibilityIssues, compatibilityScore);

        _logger.LogInformation("Compatibility check complete: {Issues} issues found, score {Score}, risk {Risk}",
            compatibilityIssues.Count, compatibilityScore, risk);

        var report = new AssessmentReport
        {
            Id = Guid.NewGuid(),
            GeneratedAt = DateTime.UtcNow,
            Schema = schema,
            Performance = performance,
            DataProfile = dataProfile,
            CompatibilityIssues = compatibilityIssues,
            CompatibilityScore = compatibilityScore,
            Risk = risk
        };

        _logger.LogInformation("Generating tier recommendation for {Server}/{Database}",
            source.Server, source.Database);
        report.Recommendation = await RecommendTierAsync(report);

        // Re-apply severity using the recommended service tier
        CompatibilityChecker.ApplyTargetSeverity(compatibilityIssues, report.Recommendation.ServiceTier);
        report.CompatibilityScore = CompatibilityChecker.CalculateCompatibilityScore(compatibilityIssues);
        report.Risk = CompatibilityChecker.DetermineRisk(compatibilityIssues, report.CompatibilityScore);

        _logger.LogInformation("Recommended tier: {Tier} ({Compute}), estimated cost ${Cost}/mo",
            report.Recommendation.ServiceTier, report.Recommendation.ComputeSize,
            report.Recommendation.EstimatedMonthlyCostUsd);

        // Generate migration strategy recommendation (default: same-platform)
        report.StrategyRecommendation = MigrationStrategyRecommender.Recommend(
            report.DataProfile, report.Schema, report.Performance, DatabasePlatform.SqlServer);

        return report;
    }

    public async Task<TierRecommendation> RecommendTierAsync(AssessmentReport report)
    {
        return await TierRecommender.RecommendAsync(
            report.Performance, report.DataProfile, report.CompatibilityScore,
            report.CompatibilityIssues, _pricingService);
    }
}
