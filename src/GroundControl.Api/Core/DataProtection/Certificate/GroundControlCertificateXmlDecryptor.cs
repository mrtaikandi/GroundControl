using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Decrypts Data Protection key XML produced by <see cref="GroundControlCertificateXmlEncryptor"/>
/// by resolving the matching certificate through <see cref="IDataProtectionCertificateProvider"/>.
/// </summary>
/// <remarks>
/// Activated by ASP.NET Core's <c>SimpleActivator</c> the first time a key needs to be decrypted.
/// That activator only supports a parameterless ctor or one that takes a single
/// <see cref="IServiceProvider"/>, so we accept the service provider and resolve our actual
/// dependencies from it.
///
/// The cryptographic recipe mirrors <c>EncryptedXmlDecryptor</c> from the framework:
/// <see cref="EncryptedXml"/> is subclassed and
/// <see cref="EncryptedXml.DecryptEncryptedKey(EncryptedKey)"/> is overridden to match the
/// embedded recipient certificate against our provider's current and previous certificates by
/// thumbprint, then unwrap the symmetric key using the matched certificate's private key.
/// </remarks>
internal sealed partial class GroundControlCertificateXmlDecryptor : IXmlDecryptor
{
    private readonly IDataProtectionCertificateProvider _provider;
    private readonly ILogger<GroundControlCertificateXmlDecryptor> _logger;

    [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Required by ASP.NET Core.")]
    public GroundControlCertificateXmlDecryptor(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _provider = services.GetRequiredService<IDataProtectionCertificateProvider>();
        _logger = services.GetRequiredService<ILogger<GroundControlCertificateXmlDecryptor>>();
    }

    // Test-only ctor that lets unit tests inject collaborators directly without building a service provider.
    internal GroundControlCertificateXmlDecryptor(
        IDataProtectionCertificateProvider provider,
        ILogger<GroundControlCertificateXmlDecryptor> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "The common algorithms are preserved by the DynamicDependency attribute.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Only XSLTs require dynamic code; the EncryptedXml usage here doesn't use XSLTs.")]
    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        var xmlDocument = new XmlDocument();
        xmlDocument.Load(new XElement("root", encryptedElement).CreateReader());

        var encryptedXml = new ProviderBackedEncryptedXml(xmlDocument, this);
        encryptedXml.DecryptDocument();

        return XElement.Load(xmlDocument.DocumentElement!.FirstChild!.CreateNavigator()!.ReadSubtree());
    }

    private X509Certificate2? FindCertificateWithPrivateKey(string thumbprint)
    {
        var current = _provider.GetCurrentCertificate();
        if (ThumbprintsMatch(current, thumbprint))
        {
            return current.HasPrivateKey ? current : null;
        }

        foreach (var previous in _provider.GetPreviousCertificates())
        {
            if (ThumbprintsMatch(previous, thumbprint))
            {
                return previous.HasPrivateKey ? previous : null;
            }
        }

        LogNoMatchingCertificate(_logger, thumbprint);
        return null;
    }

    private static bool ThumbprintsMatch(X509Certificate2 certificate, string thumbprint)
        => string.Equals(certificate.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(1, LogLevel.Warning,
        "No Data Protection certificate available with thumbprint {Thumbprint}; key XML cannot be decrypted with the current provider configuration.")]
    private static partial void LogNoMatchingCertificate(ILogger logger, string thumbprint);

    private sealed class ProviderBackedEncryptedXml : EncryptedXml
    {
        private readonly GroundControlCertificateXmlDecryptor _outer;

        public ProviderBackedEncryptedXml(XmlDocument document, GroundControlCertificateXmlDecryptor outer)
            : base(document)
        {
            _outer = outer;
        }

        public override byte[]? DecryptEncryptedKey(EncryptedKey encryptedKey)
        {
            ArgumentNullException.ThrowIfNull(encryptedKey);

            var keyInfoEnum = encryptedKey.KeyInfo.GetEnumerator();
            try
            {
                while (keyInfoEnum.MoveNext())
                {
                    if (keyInfoEnum.Current is not KeyInfoX509Data keyInfoX509Data)
                    {
                        continue;
                    }

                    var unwrapped = TryDecryptKey(encryptedKey, keyInfoX509Data);
                    if (unwrapped is not null)
                    {
                        return unwrapped;
                    }
                }

                return base.DecryptEncryptedKey(encryptedKey);
            }
            finally
            {
                (keyInfoEnum as IDisposable)?.Dispose();
            }
        }

        private byte[]? TryDecryptKey(EncryptedKey encryptedKey, KeyInfoX509Data keyInfo)
        {
            var certificateEnum = keyInfo.Certificates?.GetEnumerator();
            try
            {
                if (certificateEnum is null)
                {
                    return null;
                }

                while (certificateEnum.MoveNext())
                {
                    if (certificateEnum.Current is not X509Certificate2 embeddedCertificate)
                    {
                        continue;
                    }

                    var matchingCertificate = _outer.FindCertificateWithPrivateKey(embeddedCertificate.Thumbprint);
                    if (matchingCertificate is null)
                    {
                        continue;
                    }

                    using var privateKey = matchingCertificate.GetRSAPrivateKey();
                    if (privateKey is null)
                    {
                        continue;
                    }

                    var useOaep = encryptedKey.EncryptionMethod?.KeyAlgorithm == XmlEncRSAOAEPUrl;
                    return DecryptKey(encryptedKey.CipherData.CipherValue!, privateKey, useOaep);
                }

                return null;
            }
            finally
            {
                (certificateEnum as IDisposable)?.Dispose();
            }
        }
    }
}