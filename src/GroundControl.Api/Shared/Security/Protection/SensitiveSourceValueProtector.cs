using System.Diagnostics.CodeAnalysis;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Shared.Security.Protection;

/// <summary>
/// Centralizes encryption, decryption, and masking for plaintext-or-protected source values
/// stored on <see cref="ConfigEntry" /> and <see cref="Variable" /> entities.
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
    /// Returns <c>true</c> when <paramref name="value" /> is exactly the mask sentinel.
    /// Write paths use this to reject sensitive payloads that look like a UI round-trip of a
    /// previously masked GET response, which would otherwise overwrite the stored secret with
    /// the literal string <c>"***"</c>.
    /// </summary>
    public static bool IsMaskSentinel(string value) =>
        string.Equals(value, MaskValue, StringComparison.Ordinal);

    /// <summary>
    /// Returns a copy of <paramref name="values" /> with each non-empty value replaced by
    /// <see cref="MaskValue" /> when <paramref name="isSensitive" /> is <c>true</c>.
    /// Returns the input unchanged when not sensitive.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global", Justification = "Keep symmetric with the protect/unprotect API on this type so call sites stay uniform")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep symmetric with the protect/unprotect API on this type so call sites stay uniform")]
    public IReadOnlyList<ScopedValue> MaskValues(IEnumerable<ScopedValue> values, bool isSensitive)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!isSensitive)
        {
            return values as IReadOnlyList<ScopedValue> ?? [.. values];
        }

        return [.. values.Select(v => v with { Value = string.IsNullOrEmpty(v.Value) ? v.Value : MaskValue })];
    }

    /// <summary>
    /// Returns a copy of <paramref name="values" /> with each non-empty value passed through
    /// <see cref="IValueProtector.Protect" /> when <paramref name="isSensitive" /> is <c>true</c>.
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
    /// Returns the unprotected plaintext for a single stored value, or the value unchanged when
    /// <paramref name="isSensitive" /> is <c>false</c> or the value is empty. Used by client
    /// delivery paths (REST/SSE) that always need plaintext for authorized clients but must avoid
    /// calling <see cref="IValueProtector.Unprotect" /> on legitimately empty sensitive entries.
    /// </summary>
    public string UnprotectIfSensitive(string value, bool isSensitive)
    {
        if (!isSensitive || string.IsNullOrEmpty(value))
        {
            return value;
        }

        return _protector.Unprotect(value);
    }

    /// <summary>
    /// Returns a copy of <paramref name="values" /> with each non-empty value passed through
    /// <see cref="IValueProtector.Unprotect" /> when <paramref name="isSensitive" /> is <c>true</c>.
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
}