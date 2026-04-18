namespace GroundControl.Link;

/// <summary>
/// Protects and unprotects configuration values written to the local cache.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are supplied by the consumer via <see cref="GroundControlOptions.Protector"/>.
/// When a protector is configured, only entries the server has marked as sensitive are passed through
/// <see cref="Protect"/> on write and <see cref="Unprotect"/> on read; non-sensitive entries are
/// persisted as plaintext so they remain inspectable by diagnostic tooling.
/// </para>
/// <para>
/// The ciphertext returned by <see cref="Protect"/> is treated as opaque by the SDK. If the
/// implementation needs to support key rotation or algorithm changes, any versioning metadata
/// (key id, algorithm tag, IV, etc.) must be embedded inside the returned string and parsed
/// back out by <see cref="Unprotect"/>.
/// </para>
/// <para>
/// <see cref="Unprotect"/> must throw on any tampering or decryption failure. The SDK will
/// invalidate the on-disk cache and refetch from the server when that happens.
/// </para>
/// </remarks>
public interface IConfigurationProtector
{
    /// <summary>
    /// Encrypts a configuration value for storage in the local cache.
    /// </summary>
    /// <param name="plaintext">The configuration value to protect.</param>
    /// <returns>An opaque ciphertext string that <see cref="Unprotect"/> can reverse.</returns>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypts a configuration value previously produced by <see cref="Protect"/>.
    /// </summary>
    /// <param name="ciphertext">The opaque ciphertext.</param>
    /// <returns>The original plaintext value.</returns>
    /// <exception cref="System.Exception">
    /// Thrown on tampering, a wrong key, or an unrecognized format. The SDK treats any exception
    /// as a signal to invalidate the cache and refetch from the server.
    /// </exception>
    string Unprotect(string ciphertext);
}