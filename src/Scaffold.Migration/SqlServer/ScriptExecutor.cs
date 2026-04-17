using Microsoft.Data.SqlClient;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Migration.SqlServer;

/// <summary>
/// Executes pre/post migration SQL scripts against a target database.
/// </summary>
public class ScriptExecutor
{
    private const int DefaultScriptTimeoutSeconds = 300;
    /// <summary>
    /// Executes a list of migration scripts in order against the target connection.
    /// Scripts must have SqlContent populated before calling this method.
    /// </summary>
    public virtual async Task ExecuteScriptsAsync(
        string connectionString,
        IReadOnlyList<MigrationScript> scripts,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default,
        int? scriptTimeout = null)
    {
        var enabledScripts = scripts.Where(s => s.IsEnabled).OrderBy(s => s.Order).ToList();

        if (enabledScripts.Count == 0)
            return;

        var effectiveTimeout = ClampTimeout(scriptTimeout, DefaultScriptTimeoutSeconds);
        var phase = enabledScripts[0].Phase == MigrationScriptPhase.Pre ? "PreScripts" : "PostScripts";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        for (var i = 0; i < enabledScripts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var script = enabledScripts[i];

            progress?.Report(new MigrationProgress
            {
                Phase = phase,
                PercentComplete = (double)i / enabledScripts.Count * 100,
                Message = $"Executing: {script.Label} ({i + 1}/{enabledScripts.Count})"
            });

            if (string.IsNullOrWhiteSpace(script.SqlContent))
            {
                progress?.Report(new MigrationProgress
                {
                    Phase = phase,
                    Message = $"Skipping {script.Label}: no SQL content."
                });
                continue;
            }

            await using var cmd = new SqlCommand(script.SqlContent, connection);
            cmd.CommandTimeout = effectiveTimeout;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        progress?.Report(new MigrationProgress
        {
            Phase = phase,
            PercentComplete = 100,
            Message = $"Completed {enabledScripts.Count} {(phase == "PreScripts" ? "pre" : "post")}-migration scripts."
        });
    }

    /// <summary>
    /// Clamps a timeout value to the range [min, max], using defaultValue when value is null.
    /// </summary>
    internal static int ClampTimeout(int? value, int defaultValue, int min = 30, int max = 3600)
        => Math.Clamp(value ?? defaultValue, min, max);
}
