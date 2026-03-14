using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Api.Shared.Security.Protection;

/// <summary>
/// Protects and unprotects sensitive values using ASP.NET Core Data Protection.
/// </summary>
internal sealed class DataProtectionValueProtector : IValueProtector
{
    private const string Purpose = "GroundControl.SensitiveValues";

    private readonly IDataProtector _protector;

    public DataProtectionValueProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    /// <inheritdoc />
    public string Protect(string plainText) => _protector.Protect(plainText);

    /// <inheritdoc />
    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}