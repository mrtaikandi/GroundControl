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

        var dataProtectionBuilder = builder.Services
            .AddDataProtection()
            .SetApplicationName(builder.Environment.ApplicationName);

        if (options.CertificateProvider.HasValue)
        {
            RegisterCertificateProvider(builder.Services, options.CertificateProvider.Value);
            builder.Services.AddHostedService<CertificateStartupLogger>();
        }

        var keyRingConfigurator = CreateKeyRingConfigurator(options.Mode);
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
            var startupCertificateProvider = CreateCertificateProvider(builder.Configuration, options.CertificateProvider!.Value);
            var currentCertificate = startupCertificateProvider.GetCurrentCertificate();
            var previousCertificates = startupCertificateProvider.GetPreviousCertificates();
            dataProtectionBuilder.UnprotectKeysWithAnyCertificate([currentCertificate, .. previousCertificates]);
        }

        builder.Services.AddSingleton<IValueProtector, DataProtectionValueProtector>();
    }

    private static IKeyRingConfigurator CreateKeyRingConfigurator(DataProtectionMode mode) => mode switch
    {
        DataProtectionMode.FileSystem => new FileSystemKeyRingConfigurator(),
        DataProtectionMode.Certificate => new CertificateKeyRingConfigurator(),
        DataProtectionMode.Redis => new RedisKeyRingConfigurator(),
        DataProtectionMode.Azure => new AzureKeyRingConfigurator(),
        _ => throw new InvalidOperationException(
            $"Unknown DataProtection:Mode '{mode}'. Supported values: {string.Join(", ", Enum.GetNames<DataProtectionMode>())}.")
    };

    private static IServiceCollection RegisterCertificateProvider(IServiceCollection services, CertificateProviderMode mode) => mode switch
    {
        CertificateProviderMode.FileSystem => services.AddSingleton<IDataProtectionCertificateProvider, FileSystemCertificateProvider>(),
        CertificateProviderMode.AzureBlob => services.AddSingleton<IDataProtectionCertificateProvider, AzureBlobCertificateProvider>(),
        _ => throw new InvalidOperationException(
            $"Unknown DataProtection:CertificateProvider '{mode}'. Supported values: {string.Join(", ", Enum.GetNames<CertificateProviderMode>())}.")
    };

    private static IDataProtectionCertificateProvider CreateCertificateProvider(IConfiguration configuration, CertificateProviderMode mode) => mode switch
    {
        CertificateProviderMode.FileSystem => new FileSystemCertificateProvider(configuration, NullLogger<FileSystemCertificateProvider>.Instance),
        CertificateProviderMode.AzureBlob => new AzureBlobCertificateProvider(configuration, NullLogger<AzureBlobCertificateProvider>.Instance),
        _ => throw new InvalidOperationException(
            $"Unknown DataProtection:CertificateProvider '{mode}'. Supported values: {string.Join(", ", Enum.GetNames<CertificateProviderMode>())}.")
    };
}