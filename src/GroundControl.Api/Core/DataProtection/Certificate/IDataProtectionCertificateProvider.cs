using System.Security.Cryptography.X509Certificates;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Provides X.509 certificates for Data Protection key ring encryption.
/// </summary>
/// <remarks>
/// Implementations are invoked once at host startup, where ASP.NET Core has no
/// <c>SynchronizationContext</c>. The interface is synchronous because every consumer
/// (XmlEncryptor configuration, UnprotectKeysWithAnyCertificate wiring)
/// is itself synchronous, and certificate loading is a one-shot operation that does not
/// benefit from cancellation.
/// </remarks>
public interface IDataProtectionCertificateProvider
{
    /// <summary>
    /// Gets the current certificate used to protect newly created Data Protection keys.
    /// </summary>
    /// <returns>The active X.509 certificate.</returns>
    X509Certificate2 GetCurrentCertificate();

    /// <summary>
    /// Gets previous certificates that can still decrypt keys protected with older certificates.
    /// </summary>
    /// <returns>A list of previous certificates for key rotation, or an empty list if none exist.</returns>
    IReadOnlyList<X509Certificate2> GetPreviousCertificates();
}