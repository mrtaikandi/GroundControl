using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Shared.Security.Protection;

/// <summary>
/// Centralizes encryption, decryption, and masking for plaintext-or-protected source values
/// stored on <see cref="ConfigEntry"/> and <see cref="Variable"/> entities.
/// Empty string values are preserved verbatim — masking <c>""</c> to <c>"***"</c> would mislead
/// the UI into displaying a value that does not exist.
/// </summary>
internal sealed class SensitiveSourceValueProtector
{
    /// <summary>
    /// The mask sentinel returned in place of sensitive values when the caller is not authorized
    /// to reveal them.
    /// </summary>
    public const string MaskValue = "***";

    private readonly IValueProtector _protector;

    public SensitiveSourceValueProtector(IValueProtector protector)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    /// <summary>
    /// Returns a copy of <paramref name="values"/> with each non-empty value passed through
    /// <see cref="IValueProtector.Protect"/> when <paramref name="isSensitive"/> is <c>true</c>.
    /// Returns the input unchanged when not sensitive.
    /// </summary>
    public IReadOnlyList<ScopedValue> ProtectValues(IEnumerable<ScopedValue> values, bool isSensitive)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!isSensitive)
        {
            return values as IReadOnlyList<ScopedValue> ?? [.. values];
        }

        return [.. values.Select(v => v with { Value = string.IsNullOrEmpty(v.Value) ? v.Value : _protector.Protect(v.Value) })];
    }

    /// <summary>
    /// Returns a copy of <paramref name="values"/> with each non-empty value passed through
    /// <see cref="IValueProtector.Unprotect"/> when <paramref name="isSensitive"/> is <c>true</c>.
    /// Returns the input unchanged when not sensitive.
    /// </summary>
    public IReadOnlyList<ScopedValue> UnprotectValues(IEnumerable<ScopedValue> values, bool isSensitive)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!isSensitive)
        {
            return values as IReadOnlyList<ScopedValue> ?? [.. values];
        }

        return [.. values.Select(v => v with { Value = string.IsNullOrEmpty(v.Value) ? v.Value : _protector.Unprotect(v.Value) })];
    }

    /// <summary>
    /// Returns a copy of <paramref name="values"/> with each non-empty value replaced by
    /// <see cref="MaskValue"/> when <paramref name="isSensitive"/> is <c>true</c>.
    /// Returns the input unchanged when not sensitive.
    /// </summary>
    public static IReadOnlyList<ScopedValue> MaskValues(IEnumerable<ScopedValue> values, bool isSensitive)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!isSensitive)
        {
            return values as IReadOnlyList<ScopedValue> ?? [.. values];
        }

        return [.. values.Select(v => v with { Value = string.IsNullOrEmpty(v.Value) ? v.Value : MaskValue })];
    }
}