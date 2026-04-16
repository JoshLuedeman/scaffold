using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Api.Services;

public class PreMigrationValidator : IPreMigrationValidator
{
    public Task<PreMigrationValidationResult> ValidateAsync(MigrationPlan plan, CancellationToken ct = default)
    {
        var result = new PreMigrationValidationResult();

        if (!Enum.IsDefined(plan.Strategy))
        {
            result.Errors.Add("Migration strategy is not set or is invalid.");
        }

        if (plan.ScheduledAt.HasValue && plan.ScheduledAt.Value <= DateTime.UtcNow)
        {
            result.Errors.Add("ScheduledAt must be in the future.");
        }

        if (plan.IncludedObjects is null || plan.IncludedObjects.Count == 0)
        {
            result.Errors.Add("IncludedObjects must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(plan.SourceConnectionString))
        {
            result.Errors.Add("SourceConnectionString must not be empty.");
        }

        if (plan.PreMigrationScripts is null || plan.PreMigrationScripts.Count == 0)
        {
            result.Warnings.Add("No pre-migration scripts configured.");
        }

        if (plan.PostMigrationScripts is null || plan.PostMigrationScripts.Count == 0)
        {
            result.Warnings.Add("No post-migration scripts configured.");
        }

        return Task.FromResult(result);
    }
}
