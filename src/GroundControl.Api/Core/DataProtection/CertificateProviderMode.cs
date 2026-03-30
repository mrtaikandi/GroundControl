namespace GroundControl.Api.Core.DataProtection;

/// <summary>
/// Defines the source for X.509 certificates used in Data Protection key encryption.
/// </summary>
internal enum CertificateProviderMode
{
    /// <summary>
    /// Loads certificates from the local file system.
    /// </summary>
    FileSystem,

    /// <summary>
    /// Downloads certificates from Azure Blob Storage.
    /// </summary>
    AzureBlob
}