using Azure.Core;
using GroundControl.Api.Core.DataProtection.Certificate;
using GroundControl.Api.Core.DataProtection.KeyRing;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Host.Api;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection;

internal sealed class DataProtectionModule(DataProtectionOptions options) : IWebApiModule<DataProtectionOptions>
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        DataProtectionOptions.Validator.ThrowIfInvalid(options);

        var azureCredential = NeedsAzureCredential(options) ? AzureCredentialFactory.Create(options.AzureCredential) : null;
        if (azureCredential is not null)
        {
            builder.Services.AddSingleton(azureCredential);
        }

        var dataProtectionBuilder = builder.Services
            .AddDataProtection()
            .SetApplicationName(builder.Environment.ApplicationName);

        if (options.CertificateProvider.HasValue)
        {
            RegisterCertificateProvider(builder.Services, options);
            builder.Services.AddHostedService<CertificateStartupLogger>();
        }

        var keyRingConfigurator = CreateKeyRingConfigurator(options.Mode, azureCredential);
        keyRingConfigurator.Configure(dataProtectionBuilder, options);

        if (options.Mode is DataProtectionMode.Certificate or DataProtectionMode.Redis)
        {
            builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>, CertificateKeyEncryptionConfigurator>();

            // The decryption side of certificate-based key ring protection cannot be wired through
            // IConfigureOptions because XmlKeyDecryptionOptions is internal to ASP.NET Core. The only
            // public surface is UnprotectKeysWithAnyCertificate, which captures the certificates at
            // registration time. Loading them here and supplying both current and previous certs is
            // required for cross-instance and cross-restart decryption to work, and for safe
            // certificate rotation.
            var startupCertificateProvider = CreateCertificateProvider(options, azureCredential);
            var currentCertificate = startupCertificateProvider.GetCurrentCertificate();
            var previousCertificates = startupCertificateProvider.GetPreviousCertificates();
            dataProtectionBuilder.UnprotectKeysWithAnyCertificate([currentCertificate, .. previousCertificates]);
        }

        builder.Services.AddSingleton<IValueProtector, DataProtectionValueProtector>();
    }

    private static bool NeedsAzureCredential(DataProtectionOptions options) =>
        options.Mode is DataProtectionMode.Azure || options.CertificateProvider is CertificateProviderMode.AzureBlob;

    private static IKeyRingConfigurator CreateKeyRingConfigurator(DataProtectionMode mode, TokenCredential? azureCredential) => mode switch
    {
        DataProtectionMode.FileSystem => new FileSystemKeyRingConfigurator(),
        DataProtectionMode.Certificate => new CertificateKeyRingConfigurator(),
        DataProtectionMode.Redis => new RedisKeyRingConfigurator(),
        DataProtectionMode.Azure => new AzureKeyRingConfigurator(azureCredential ?? throw new InvalidOperationException(
            $"An Azure credential is required when {nameof(DataProtectionOptions)}:{nameof(DataProtectionOptions.Mode)} is '{mode}'.")),
        _ => throw new InvalidOperationException(
            $"Unknown {nameof(DataProtectionOptions)}:{nameof(DataProtectionOptions.Mode)} '{mode}'. Supported values: {string.Join(", ", Enum.GetNames<DataProtectionMode>())}.")
    };

    private static void RegisterCertificateProvider(IServiceCollection services, DataProtectionOptions options)
    {
        switch (options.CertificateProvider!.Value)
        {
            case CertificateProviderMode.FileSystem:
                services.AddSingleton(Options.Create(options.FileSystemCertificate));
                services.AddSingleton<IDataProtectionCertificateProvider, FileSystemCertificateProvider>();
                break;

            case CertificateProviderMode.AzureBlob:
                services.AddSingleton(Options.Create(options.AzureBlobCertificate));
                services.AddSingleton<IDataProtectionCertificateProvider, AzureBlobCertificateProvider>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown {nameof(DataProtectionOptions)}:{nameof(DataProtectionOptions.CertificateProvider)} '{options.CertificateProvider}'. Supported values: {string.Join(", ", Enum.GetNames<CertificateProviderMode>())}.");
        }
    }

    private static IDataProtectionCertificateProvider CreateCertificateProvider(DataProtectionOptions options, TokenCredential? azureCredential) => options.CertificateProvider switch
    {
        CertificateProviderMode.FileSystem => new FileSystemCertificateProvider(Options.Create(options.FileSystemCertificate), NullLogger<FileSystemCertificateProvider>.Instance),
        CertificateProviderMode.AzureBlob => new AzureBlobCertificateProvider(
            Options.Create(options.AzureBlobCertificate),
            azureCredential ?? throw new InvalidOperationException(
                $"An Azure credential is required when {nameof(DataProtectionOptions)}:{nameof(DataProtectionOptions.CertificateProvider)} is '{options.CertificateProvider}'."),
            NullLogger<AzureBlobCertificateProvider>.Instance),
        _ => throw new InvalidOperationException(
            $"Unknown {nameof(DataProtectionOptions)}:{nameof(DataProtectionOptions.CertificateProvider)} '{options.CertificateProvider}'. Supported values: {string.Join(", ", Enum.GetNames<CertificateProviderMode>())}.")
    };
}