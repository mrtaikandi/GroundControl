using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GroundControl.Api.Core.DataProtection.Certificate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

/// <summary>
/// Verifies <see cref="AzureBlobCertificateProvider"/> downloads PFX bytes via the injected
/// <c>BlobClient</c> factory and loads them as X.509 certificates. We mock <c>BlobClient</c>
/// directly: the production network/auth path is the Azure SDK's responsibility, and Azurite's
/// API version trails the SDK package the project pins, so an integration container would be
/// flaky. The provider's own logic — choose blob → download → decode PFX — is fully covered here.
/// </summary>
public sealed class AzureBlobCertificateProviderTests
{
    private readonly TokenCredential _credential = Substitute.For<TokenCredential>();
    private readonly ILogger<AzureBlobCertificateProvider> _logger = NullLogger<AzureBlobCertificateProvider>.Instance;

    [Fact]
    public void GetCurrentCertificate_DownloadsAndLoadsBlob_NoPassword()
    {
        // Arrange
        var pfxBytes = CreateSelfSignedPfxBytes(password: null);
        var blobUri = new Uri("https://account.blob.core.windows.net/dp-certs/current.pfx");
        var provider = CreateProvider(blobUri, password: null, downloadResponse: BuildDownloadResponse(pfxBytes));

        // Act
        using var certificate = provider.GetCurrentCertificate();

        // Assert
        certificate.HasPrivateKey.ShouldBeTrue();
        certificate.Thumbprint.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GetCurrentCertificate_DownloadsAndLoadsBlob_WithPassword()
    {
        // Arrange
        const string Password = "blob-pfx-password";
        var pfxBytes = CreateSelfSignedPfxBytes(Password);
        var blobUri = new Uri("https://account.blob.core.windows.net/dp-certs/current.pfx");
        var provider = CreateProvider(blobUri, password: Password, downloadResponse: BuildDownloadResponse(pfxBytes));

        // Act
        using var certificate = provider.GetCurrentCertificate();

        // Assert
        certificate.HasPrivateKey.ShouldBeTrue();
    }

    [Fact]
    public void GetCurrentCertificate_WrongPassword_ThrowsCryptographicException()
    {
        // Arrange
        var pfxBytes = CreateSelfSignedPfxBytes(password: "correct-password");
        var blobUri = new Uri("https://account.blob.core.windows.net/dp-certs/current.pfx");
        var provider = CreateProvider(blobUri, password: "wrong-password", downloadResponse: BuildDownloadResponse(pfxBytes));

        // Act + Assert
        Should.Throw<CryptographicException>(() => provider.GetCurrentCertificate());
    }

    [Fact]
    public void GetCurrentCertificate_BlobClientThrows_PropagatesException()
    {
        // Arrange — A 404-style failure surfacing through the real BlobClient implementation.
        var blobUri = new Uri("https://account.blob.core.windows.net/dp-certs/missing.pfx");
        var blobClient = MockBlobClientThrowing(new RequestFailedException(404, "Not Found"));

        var options = new AzureBlobCertificateOptions
        {
            BlobUri = blobUri,
            PreviousBlobUris = []
        };
        var provider = new AzureBlobCertificateProvider(
            Options.Create(options),
            _credential,
            _logger,
            (_, _) => blobClient);

        // Act + Assert
        Should.Throw<RequestFailedException>(() => provider.GetCurrentCertificate());
    }

    [Fact]
    public void GetPreviousCertificates_NoneConfigured_ReturnsEmpty()
    {
        // Arrange
        var options = new AzureBlobCertificateOptions
        {
            BlobUri = new Uri("https://account.blob.core.windows.net/dp-certs/current.pfx"),
            PreviousBlobUris = []
        };
        var provider = new AzureBlobCertificateProvider(
            Options.Create(options),
            _credential,
            _logger,
            (_, _) => Substitute.For<BlobClient>());

        // Act
        var result = provider.GetPreviousCertificates();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetPreviousCertificates_LoadsAllConfiguredBlobs()
    {
        // Arrange
        var firstUri = new Uri("https://account.blob.core.windows.net/dp-certs/prev-1.pfx");
        var secondUri = new Uri("https://account.blob.core.windows.net/dp-certs/prev-2.pfx");
        var firstClient = BuildBlobClient(BuildDownloadResponse(CreateSelfSignedPfxBytes(password: null)));
        var secondClient = BuildBlobClient(BuildDownloadResponse(CreateSelfSignedPfxBytes(password: null)));

        var clientsByUri = new Dictionary<Uri, BlobClient>
        {
            [firstUri] = firstClient,
            [secondUri] = secondClient
        };

        var options = new AzureBlobCertificateOptions
        {
            BlobUri = firstUri,
            PreviousBlobUris = [firstUri, secondUri]
        };
        var provider = new AzureBlobCertificateProvider(
            Options.Create(options),
            _credential,
            _logger,
            (uri, _) => clientsByUri[uri]);

        // Act
        var certificates = provider.GetPreviousCertificates();

        // Assert
        certificates.Count.ShouldBe(2);
        certificates.ShouldAllBe(c => c.HasPrivateKey);

        foreach (var cert in certificates)
        {
            cert.Dispose();
        }
    }

    private AzureBlobCertificateProvider CreateProvider(Uri blobUri, string? password, Response<BlobDownloadResult> downloadResponse)
    {
        var options = new AzureBlobCertificateOptions
        {
            BlobUri = blobUri,
            Password = password,
            PreviousBlobUris = []
        };

        return new AzureBlobCertificateProvider(
            Options.Create(options),
            _credential,
            _logger,
            (_, _) => BuildBlobClient(downloadResponse));
    }

    [SuppressMessage(
        "Usage",
        "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken",
        Justification = "The mocked overload must match the parameterless DownloadContent the production code invokes.")]
    private static BlobClient BuildBlobClient(Response<BlobDownloadResult> downloadResponse)
    {
        var client = Substitute.For<BlobClient>();
        client.DownloadContent().Returns(downloadResponse);
        return client;
    }

    [SuppressMessage(
        "Usage",
        "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken",
        Justification = "The mocked overload must match the parameterless DownloadContent the production code invokes.")]
    private static BlobClient MockBlobClientThrowing(Exception exception)
    {
        var client = Substitute.For<BlobClient>();
        client.DownloadContent().Returns<Response<BlobDownloadResult>>(_ => throw exception);
        return client;
    }

    private static Response<BlobDownloadResult> BuildDownloadResponse(byte[] pfxBytes)
    {
        var result = BlobsModelFactory.BlobDownloadResult(BinaryData.FromBytes(pfxBytes));
        var response = Substitute.For<Response>();
        return Response.FromValue(result, response);
    }

    private static byte[] CreateSelfSignedPfxBytes(string? password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=GroundControl Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        return certificate.Export(X509ContentType.Pfx, password);
    }
}