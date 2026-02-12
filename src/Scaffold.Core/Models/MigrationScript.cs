using Scaffold.Core.Enums;

namespace Scaffold.Core.Models;

public class MigrationScript
{
    public string ScriptId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public MigrationScriptType ScriptType { get; set; }
    public MigrationScriptPhase Phase { get; set; }
    public string SqlContent { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int Order { get; set; }
}
