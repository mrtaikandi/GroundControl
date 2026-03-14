namespace GroundControl.Api.Shared.Security.Protection;

/// <summary>
/// Encrypts and decrypts sensitive configuration values.
/// </summary>
public interface IValueProtector
{
    /// <summary>
    /// Encrypts the specified plain text value.
    /// </summary>
    /// <param name="plainText">The value to encrypt.</param>
    /// <returns>The encrypted representation of the value.</returns>
    string Protect(string plainText);

    /// <summary>
    /// Decrypts the specified protected value.
    /// </summary>
    /// <param name="protectedText">The encrypted value to decrypt.</param>
    /// <returns>The original plain text value.</returns>
    string Unprotect(string protectedText);
}