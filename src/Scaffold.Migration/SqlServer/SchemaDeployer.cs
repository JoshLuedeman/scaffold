using Microsoft.SqlServer.Dac;
using Scaffold.Core.Interfaces;

namespace Scaffold.Migration.SqlServer;

/// <summary>
/// Extracts a DACPAC from the source SQL Server and deploys it to the target Azure SQL.
/// </summary>
public class SchemaDeployer
{
    public virtual async Task DeploySchemaAsync(
        string sourceConnectionString,
        string targetConnectionString,
        string databaseName,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var dacpacPath = Path.Combine(Path.GetTempPath(), $"{databaseName}_{Guid.NewGuid():N}.dacpac");

        try
        {
            // Step 1: Extract schema from source as DACPAC
            progress?.Report(new MigrationProgress
            {
                Phase = "SchemaExtract",
                PercentComplete = 0,
                Message = "Extracting schema from source database..."
            });

            await Task.Run(() =>
            {
                var extractService = new DacServices(sourceConnectionString);
                extractService.Message += (sender, e) =>
                {
                    progress?.Report(new MigrationProgress
                    {
                        Phase = "SchemaExtract",
                        PercentComplete = 10,
                        Message = e.Message.Message
                    });
                };

                extractService.Extract(dacpacPath, databaseName, databaseName, Version.Parse("1.0.0"));
            }, ct);

            ct.ThrowIfCancellationRequested();

            progress?.Report(new MigrationProgress
            {
                Phase = "SchemaExtract",
                PercentComplete = 40,
                Message = "Schema extraction complete."
            });

            // Step 2: Deploy DACPAC to target Azure SQL
            progress?.Report(new MigrationProgress
            {
                Phase = "SchemaDeploy",
                PercentComplete = 50,
                Message = "Deploying schema to target database..."
            });

            await Task.Run(() =>
            {
                var deployService = new DacServices(targetConnectionString);
                deployService.Message += (sender, e) =>
                {
                    progress?.Report(new MigrationProgress
                    {
                        Phase = "SchemaDeploy",
                        PercentComplete = 70,
                        Message = e.Message.Message
                    });
                };

                using var package = DacPackage.Load(dacpacPath);

                var options = new DacDeployOptions
                {
                    BlockOnPossibleDataLoss = false,
                    DropObjectsNotInSource = false,
                    IgnorePermissions = true,
                    IgnoreRoleMembership = true
                };

                deployService.Deploy(package, databaseName, upgradeExisting: true, options: options);
            }, ct);

            ct.ThrowIfCancellationRequested();

            progress?.Report(new MigrationProgress
            {
                Phase = "SchemaDeploy",
                PercentComplete = 100,
                Message = "Schema deployment complete."
            });
        }
        finally
        {
            if (File.Exists(dacpacPath))
                File.Delete(dacpacPath);
        }
    }

    /// <summary>
    /// Extracts the target database name from a connection string.
    /// </summary>
    public static string ExtractDatabaseName(string connectionString)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        return builder.InitialCatalog;
    }
}
