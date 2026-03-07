namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a query for listing clients.
/// </summary>
public class ClientListQuery : ListQuery
{
    /// <summary>
    /// Gets or sets the project identifier whose clients should be listed.
    /// </summary>
    public required Guid ProjectId { get; set; }
}