using Npgsql;
using Scaffold.Core.Interfaces;
using Scaffold.Migration.PostgreSql.Models;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Handles PostgreSQL extension compatibility during PG → Azure PG migrations.
/// Determines which extensions can be installed, skips unsupported ones with warnings,
/// and installs extensions in dependency order.
/// </summary>
public class AzureExtensionHandler
{
    /// <summary>
    /// Evaluates which source extensions can be migrated to Azure PG.
    /// Returns a plan with supported extensions in dependency order and unsupported ones with warnings.
    /// </summary>
    public ExtensionMigrationPlan EvaluateExtensions(IReadOnlyList<string> sourceExtensions)
    {
        var plan = new ExtensionMigrationPlan();

        foreach (var ext in sourceExtensions)
        {
            if (AzurePostgreSqlExtensions.IsSupported(ext))
            {
                plan.ToInstall.Add(ext);

                if (AzurePostgreSqlExtensions.RequiresSharedPreload(ext))
                {
                    plan.Warnings.Add(new ExtensionWarning
                    {
                        ExtensionName = ext,
                        Message = $"Extension '{ext}' requires shared_preload_libraries configuration. " +
                                  "Ensure the Azure PG server parameter is set before installation (requires restart).",
                        Severity = ExtensionWarningSeverity.Info
                    });
                }
            }
            else
            {
                plan.Skipped.Add(ext);
                plan.Warnings.Add(new ExtensionWarning
                {
                    ExtensionName = ext,
                    Message = $"Extension '{ext}' is not supported on Azure Database for PostgreSQL - Flexible Server and will be skipped.",
                    Severity = ExtensionWarningSeverity.Warning
                });
            }
        }

        // Order ToInstall by dependencies: dependencies must come first
        plan.ToInstall = OrderByDependencies(plan.ToInstall);

        return plan;
    }

    /// <summary>
    /// Installs supported extensions on the target in dependency order.
    /// Skips unsupported extensions with warnings. Reports progress.
    /// </summary>
    public virtual async Task<ExtensionMigrationResult> InstallExtensionsAsync(
        string targetConnectionString,
        IReadOnlyList<string> sourceExtensions,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var plan = EvaluateExtensions(sourceExtensions);
        var result = new ExtensionMigrationResult
        {
            Skipped = plan.Skipped,
            Warnings = new List<ExtensionWarning>(plan.Warnings)
        };

        if (plan.ToInstall.Count == 0)
        {
            progress?.Report(new MigrationProgress
            {
                Phase = "ExtensionInstallation",
                PercentComplete = 100,
                Message = "No extensions to install."
            });
            return result;
        }

        await using var connection = new NpgsqlConnection(targetConnectionString);
        await connection.OpenAsync(ct);

        for (var i = 0; i < plan.ToInstall.Count; i++)
        {
            var ext = plan.ToInstall[i];
            var pct = (double)(i + 1) / plan.ToInstall.Count * 100;

            progress?.Report(new MigrationProgress
            {
                Phase = "ExtensionInstallation",
                PercentComplete = pct,
                Message = $"Installing extension '{ext}' ({i + 1}/{plan.ToInstall.Count})..."
            });

            try
            {
                var quotedExt = PgIdentifierHelper.QuoteIdentifier(ext);
                var sql = $"CREATE EXTENSION IF NOT EXISTS {quotedExt}";

                await using var cmd = new NpgsqlCommand(sql, connection);
                cmd.CommandTimeout = 60;
                await cmd.ExecuteNonQueryAsync(ct);

                result.Installed.Add(ext);
            }
            catch (Exception ex)
            {
                result.Failed.Add(ext);
                result.Warnings.Add(new ExtensionWarning
                {
                    ExtensionName = ext,
                    Message = $"Failed to install extension '{ext}': {ex.Message}",
                    Severity = ExtensionWarningSeverity.Error
                });
            }
        }

        progress?.Report(new MigrationProgress
        {
            Phase = "ExtensionInstallation",
            PercentComplete = 100,
            Message = $"Extension installation complete. Installed: {result.Installed.Count}, " +
                      $"Skipped: {result.Skipped.Count}, Failed: {result.Failed.Count}."
        });

        return result;
    }

    /// <summary>
    /// Orders extensions so dependencies come before dependents.
    /// Uses a simple topological approach based on known dependency chains.
    /// </summary>
    internal static List<string> OrderByDependencies(List<string> extensions)
    {
        var extensionSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ext in extensions)
        {
            Visit(ext, extensionSet, visited, ordered);
        }

        return ordered;
    }

    private static void Visit(
        string ext,
        HashSet<string> extensionSet,
        HashSet<string> visited,
        List<string> ordered)
    {
        if (visited.Contains(ext))
            return;

        visited.Add(ext);

        // Visit dependencies first
        var deps = AzurePostgreSqlExtensions.GetDependencies(ext);
        foreach (var dep in deps)
        {
            if (extensionSet.Contains(dep))
            {
                Visit(dep, extensionSet, visited, ordered);
            }
        }

        ordered.Add(ext);
    }
}
