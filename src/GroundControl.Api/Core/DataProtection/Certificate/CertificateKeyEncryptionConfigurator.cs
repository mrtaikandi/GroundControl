using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Configures Data Protection key encryption using X.509 certificates resolved from DI.
/// </summary>
/// <remarks>
/// <para>
/// This defers certificate loading from service registration time to the first resolution of
/// <see cref="KeyManagementOptions"/>. ASP.NET Core Data Protection is synchronous by design
/// (see aspnetcore#3548), so the blocking call to the async certificate provider is unavoidable.
/// It is safe because ASP.NET Core has no <c>SynchronizationContext</c>.
/// </para>
/// <para>
/// The certificate provider is resolved from DI with proper logging, replacing the previous
/// approach of manually constructing providers with <c>NullLoggerFactory</c>.
/// </para>
/// </remarks>
internal sealed class CertificateKeyEncryptionConfigurator(
    IDataProtectionCertificateProvider certificateProvider,
    ILoggerFactory loggerFactory) : IConfigureOptions<KeyManagementOptions>
{
    /// <inheritdoc />
    public void Configure(KeyManagementOptions options)
    {
        var certificate = certificateProvider.GetCurrentCertificateAsync().GetAwaiter().GetResult();
        options.XmlEncryptor = new CertificateXmlEncryptor(certificate, loggerFactory);
    }
}