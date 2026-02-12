namespace Scaffold.Core.Enums;

public enum CompatibilitySeverity
{
    Supported,      // Feature works with no changes on this target
    Partial,        // Feature works with limitations — review docs
    Unsupported     // Feature is not available — must remediate
}
