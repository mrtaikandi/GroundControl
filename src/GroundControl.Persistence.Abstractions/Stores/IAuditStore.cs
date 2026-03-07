using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for audit records. Audit records are append-only.
/// </summary>
public interface IAuditStore
{
    /// <summary>
    /// Creates a new audit record.
    /// </summary>
    Task CreateAsync(AuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an audit record by its unique identifier.
    /// </summary>
    Task<AuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists audit records with paging, sorting, and optional filters.
    /// </summary>
    Task<PagedResult<AuditRecord>> ListAsync(AuditListQuery query, CancellationToken cancellationToken = default);
}