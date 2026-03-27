using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Shared.Masking;

internal sealed class SensitiveValueMasker
{
    private const string Mask = "***";

    private readonly IValueProtector _protector;
    private readonly AuditRecorder _audit;

    public SensitiveValueMasker(IValueProtector protector, AuditRecorder audit)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public static bool CanDecrypt(HttpContext httpContext, bool decryptRequested) =>
        decryptRequested && httpContext.User.HasClaim("permission", Permissions.SensitiveValuesDecrypt);

    public string MaskOrDecrypt(string value, bool isSensitive, bool canDecrypt)
    {
        if (!isSensitive)
        {
            return value;
        }

        return canDecrypt ? _protector.Unprotect(value) : Mask;
    }

    public async Task<IReadOnlyList<ScopedValue>> MaskOrDecryptAsync(
        IEnumerable<ScopedValue> values,
        bool isSensitive,
        bool canDecrypt,
        string entityType,
        Guid entityId,
        Guid? groupId,
        CancellationToken cancellationToken = default)
    {
        var list = values as IReadOnlyList<ScopedValue> ?? values.ToList();

        if (!isSensitive)
        {
            return list;
        }

        if (canDecrypt)
        {
            await _audit.RecordAsync(entityType, entityId, groupId, "Decrypted", cancellationToken: cancellationToken).ConfigureAwait(false);

            // ConfigEntry/Variable values are stored in plaintext — just reveal them.
            // Snapshot values are encrypted and use MaskOrDecrypt (sync) with Unprotect instead.
            return list;
        }

        return list.Select(v => v with { Value = Mask }).ToList();
    }
}