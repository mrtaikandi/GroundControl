using Azure.Core;
using GroundControl.Api.Core.DataProtection.Certificate;
using GroundControl.Api.Core.DataProtection.KeyRing;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Host.Api;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
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
            // GroundControlCertificateXmlEncryptor pins GroundControlCertificateXmlDecryptor into
            // the persisted key XML, so the decryption side runs through DI on first use — no
            // eager certificate load, no UnprotectKeysWithAnyCertificate, no XmlKeyDecryptionOptions
            // plumbing. The decryptor itself is activated by the Data Protection IActivator and
            // does not need explicit DI registration; only its constructor dependencies do.
            builder.Services.AddSingleton<GroundControlCertificateXmlEncryptor>();
            builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>, CertificateKeyEncryptionConfigurator>();
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
}