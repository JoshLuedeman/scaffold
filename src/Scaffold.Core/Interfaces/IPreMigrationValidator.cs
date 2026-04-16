using Scaffold.Core.Models;

namespace Scaffold.Core.Interfaces;

public interface IPreMigrationValidator
{
    Task<PreMigrationValidationResult> ValidateAsync(MigrationPlan plan, CancellationToken ct = default);
}
