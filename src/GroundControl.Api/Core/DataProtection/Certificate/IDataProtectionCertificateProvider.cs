using System.Security.Cryptography.X509Certificates;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Provides X.509 certificates for Data Protection key ring encryption.
/// </summary>
public interface IDataProtectionCertificateProvider
{
    /// <summary>
    /// Gets the current certificate used to protect newly created Data Protection keys.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The active X.509 certificate.</returns>
    Task<X509Certificate2> GetCurrentCertificateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets previous certificates that can still decrypt keys protected with older certificates.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of previous certificates for key rotation, or an empty list if none exist.</returns>
    Task<IReadOnlyList<X509Certificate2>> GetPreviousCertificatesAsync(CancellationToken cancellationToken = default);
}