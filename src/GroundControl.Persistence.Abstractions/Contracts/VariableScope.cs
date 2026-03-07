namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Defines the ownership scope for a variable.
/// </summary>
public enum VariableScope
{
    /// <summary>
    /// The variable is available globally, optionally within a group.
    /// </summary>
    Global,

    /// <summary>
    /// The variable belongs to a specific project.
    /// </summary>
    Project
}