using System.Security.Claims;
using System.Text.Json;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Shared.Audit;

internal sealed class AuditRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IAuditStore _auditStore;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditRecorder(IAuditStore auditStore, IHttpContextAccessor httpContextAccessor)
    {
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public async Task RecordAsync(
        string entityType,
        Guid entityId,
        Guid? groupId,
        string action,
        IReadOnlyList<FieldChange>? changes = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var performedBy = ResolveActor();
        var record = new AuditRecord
        {
            Id = Guid.CreateVersion7(),
            EntityType = entityType,
            EntityId = entityId,
            GroupId = groupId,
            Action = action,
            PerformedBy = performedBy,
            PerformedAt = DateTimeOffset.UtcNow,
            Changes = changes is not null ? [.. changes] : [],
            Metadata = metadata is not null ? new Dictionary<string, string>(metadata) : []
        };

        await _auditStore.CreateAsync(record, cancellationToken).ConfigureAwait(false);
    }

    internal static List<FieldChange> CompareCollections<T>(string field, IReadOnlyCollection<T> oldValues, IReadOnlyCollection<T> newValues, bool isSensitive = false)
    {
        var oldJson = JsonSerializer.Serialize(oldValues, SerializerOptions);
        var newJson = JsonSerializer.Serialize(newValues, SerializerOptions);

        if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
        {
            return [];
        }

        if (isSensitive)
        {
            return [new FieldChange { Field = field, OldValue = "***", NewValue = "***" }];
        }

        return [new FieldChange { Field = field, OldValue = oldJson, NewValue = newJson }];
    }

    internal static List<FieldChange> CompareFields(string field, string? oldValue, string? newValue, bool isSensitive = false)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return [];
        }

        if (isSensitive)
        {
            return [new FieldChange { Field = field, OldValue = "***", NewValue = "***" }];
        }

        return [new FieldChange { Field = field, OldValue = oldValue, NewValue = newValue }];
    }

    internal static List<FieldChange> CompareFields<T>(string field, T? oldValue, T? newValue, bool isSensitive = false)
        where T : struct, IEquatable<T>
    {
        if (EqualityComparer<T?>.Default.Equals(oldValue, newValue))
        {
            return [];
        }

        if (isSensitive)
        {
            return [new FieldChange { Field = field, OldValue = "***", NewValue = "***" }];
        }

        return [new FieldChange { Field = field, OldValue = oldValue?.ToString(), NewValue = newValue?.ToString() }];
    }

    private Guid ResolveActor()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var sub = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var userId) ? userId : Guid.Empty;
    }
}