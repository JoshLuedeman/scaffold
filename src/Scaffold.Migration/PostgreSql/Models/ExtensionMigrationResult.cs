namespace Scaffold.Migration.PostgreSql.Models;

/// <summary>
/// Plan for migrating PostgreSQL extensions to Azure Flexible Server.
/// Separates supported extensions (to install) from unsupported ones (to skip).
/// </summary>
public class ExtensionMigrationPlan
{
    /// <summary>Supported extensions that will be installed, in dependency order.</summary>
    public List<string> ToInstall { get; set; } = [];

    /// <summary>Unsupported extensions that will be skipped.</summary>
    public List<string> Skipped { get; set; } = [];

    /// <summary>Warnings generated during evaluation.</summary>
    public List<ExtensionWarning> Warnings { get; set; } = [];
}

/// <summary>
/// Result of installing PostgreSQL extensions on an Azure target.
/// </summary>
public class ExtensionMigrationResult
{
    /// <summary>Extensions successfully installed.</summary>
    public List<string> Installed { get; set; } = [];

    /// <summary>Extensions skipped (unsupported).</summary>
    public List<string> Skipped { get; set; } = [];

    /// <summary>Extensions that failed to install.</summary>
    public List<string> Failed { get; set; } = [];

    /// <summary>Warnings generated during installation.</summary>
    public List<ExtensionWarning> Warnings { get; set; } = [];

    /// <summary>True if no extensions failed to install.</summary>
    public bool Success => Failed.Count == 0;
}

/// <summary>
/// Warning about an extension during migration evaluation or installation.
/// </summary>
public class ExtensionWarning
{
    public string ExtensionName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ExtensionWarningSeverity Severity { get; set; }
}

/// <summary>
/// Severity level for extension warnings.
/// </summary>
public enum ExtensionWarningSeverity
{
    Info,
    Warning,
    Error
}
