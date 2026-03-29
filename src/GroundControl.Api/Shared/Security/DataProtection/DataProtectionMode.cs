namespace GroundControl.Api.Shared.Security.DataProtection;

/// <summary>
/// Defines the Data Protection key ring storage mode.
/// </summary>
internal enum DataProtectionMode
{
    /// <summary>
    /// Persists keys to the local file system. Optionally protects with DPAPI on Windows.
    /// </summary>
    FileSystem,

    /// <summary>
    /// Persists keys to the local file system and protects them with an X.509 certificate.
    /// </summary>
    Certificate,

    /// <summary>
    /// Persists keys to Redis and protects them with an X.509 certificate.
    /// </summary>
    Redis,

    /// <summary>
    /// Persists keys to Azure Blob Storage and protects them with Azure Key Vault.
    /// </summary>
    Azure
}