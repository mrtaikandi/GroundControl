using GroundControl.Api.Shared.Audit;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Shared.Security.Protection;

internal sealed class SensitiveValueMasker
{
    private readonly AuditRecorder _audit;
    private readonly IValueProtector _protector;
    private readonly SensitiveSourceValueProtector _sourceProtector;

    public SensitiveValueMasker(IValueProtector protector, SensitiveSourceValueProtector sourceProtector, AuditRecorder audit)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _sourceProtector = sourceProtector ?? throw new ArgumentNullException(nameof(sourceProtector));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static bool CanDecrypt(HttpContext httpContext, bool decryptRequested) =>
        decryptRequested && httpContext.User.HasClaim("permission", Permissions.SensitiveValuesDecrypt);

    /// <summary>
    /// Masks or decrypts a single encrypted value (used for snapshot entries that audit once
    /// per request rather than per value).
    /// </summary>
    public string MaskOrDecrypt(string value, bool isSensitive, bool canDecrypt)
    {
        if (!isSensitive || string.IsNullOrEmpty(value))
        {
            return value;
        }

        return canDecrypt ? _protector.Unprotect(value) : SensitiveSourceValueProtector.MaskValue;
    }

    /// <summary>
    /// Masks or decrypts a collection of stored sensitive values for a config entry or variable.
    /// Sensitive source values are stored encrypted; this method calls
    /// <see cref="IValueProtector.Unprotect"/> when reveal is permitted and records a reveal audit
    /// entry. Empty values are preserved verbatim — there is no stored content to hide.
    /// </summary>
    public async Task<IReadOnlyList<ScopedValue>> MaskOrDecryptAsync(
        IEnumerable<ScopedValue> values,
        bool isSensitive,
        bool canDecrypt,
        string entityType,
        Guid entityId,
        Guid? groupId,
        CancellationToken cancellationToken = default)
    {
        var list = values as IReadOnlyList<ScopedValue> ?? [.. values];

        if (!isSensitive)
        {
            return list;
        }

        if (canDecrypt)
        {
            await _audit.RecordAsync(entityType, entityId, groupId, "Decrypted", cancellationToken: cancellationToken).ConfigureAwait(false);
            return _sourceProtector.UnprotectValues(list, isSensitive: true);
        }

        return SensitiveSourceValueProtector.MaskValues(list, isSensitive: true);
    }
}