namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Defines the owner type for a configuration entry.
/// </summary>
public enum ConfigEntryOwnerType
{
    /// <summary>
    /// The configuration entry belongs to a template.
    /// </summary>
    Template,

    /// <summary>
    /// The configuration entry belongs to a project.
    /// </summary>
    Project
}