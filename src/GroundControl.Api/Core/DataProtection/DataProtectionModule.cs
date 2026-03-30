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

        var dataProtectionBuilder = builder.Services
            .AddDataProtection()
            .SetApplicationName(builder.Environment.ApplicationName);

        if (options.CertificateProvider.HasValue)
        {
            RegisterCertificateProvider(builder.Services, options.CertificateProvider.Value)
                .AddHostedService<CertificateStartupLogger>();
        }

        var keyRingConfigurator = CreateKeyRingConfigurator(options.Mode);
        keyRingConfigurator.Configure(dataProtectionBuilder, options);

        if (options.Mode is DataProtectionMode.Certificate or DataProtectionMode.Redis)
        {
            builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>, CertificateKeyEncryptionConfigurator>();
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
}