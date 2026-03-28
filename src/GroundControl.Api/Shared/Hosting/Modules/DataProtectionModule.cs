using GroundControl.Api.Shared.Security.Certificate;
using GroundControl.Api.Shared.Security.KeyRing;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Host.Api;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GroundControl.Api.Shared.Hosting.Modules;

[RunsAfter<ConfigurationModule>(Required = true)]
internal sealed class DataProtectionModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        var dataProtectionBuilder = builder.Services.AddDataProtection()
            .SetApplicationName("GroundControl");

        var certProviderMode = builder.Configuration["DataProtection:CertificateProvider"];
        if (certProviderMode is not null)
        {
            RegisterCertificateProvider(certProviderMode, builder.Services);
            builder.Services.AddHostedService<CertificateStartupLogger>();
        }

        var dpMode = builder.Configuration["DataProtection:Mode"] ?? "FileSystem";
        IKeyRingConfigurator keyRingConfigurator;

        if (string.Equals(dpMode, "FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            keyRingConfigurator = new FileSystemKeyRingConfigurator();
        }
        else if (string.Equals(dpMode, "Certificate", StringComparison.OrdinalIgnoreCase))
        {
            keyRingConfigurator = new CertificateKeyRingConfigurator(RequireCertificateProvider(certProviderMode, dpMode, builder.Configuration));
        }
        else if (string.Equals(dpMode, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            keyRingConfigurator = new RedisKeyRingConfigurator(RequireCertificateProvider(certProviderMode, dpMode, builder.Configuration));
        }
        else if (string.Equals(dpMode, "Azure", StringComparison.OrdinalIgnoreCase))
        {
            keyRingConfigurator = new AzureKeyRingConfigurator();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown DataProtection:Mode '{dpMode}'. Supported values are 'FileSystem', 'Certificate', 'Redis', and 'Azure'.");
        }

        keyRingConfigurator.Configure(dataProtectionBuilder, builder.Configuration);
        builder.Services.AddHostedService(sp =>
            new KeyRingStartupLogger(dpMode, sp.GetRequiredService<ILogger<KeyRingStartupLogger>>()));

        builder.Services.AddSingleton<IValueProtector, DataProtectionValueProtector>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
    }

    private static void RegisterCertificateProvider(string mode, IServiceCollection services)
    {
        if (string.Equals(mode, "FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IDataProtectionCertificateProvider, FileSystemCertificateProvider>();
        }
        else if (string.Equals(mode, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IDataProtectionCertificateProvider, AzureBlobCertificateProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown DataProtection:CertificateProvider mode: '{mode}'. Supported values are 'FileSystem' and 'AzureBlob'.");
        }
    }

    // Constructs a certificate provider for key ring configuration at startup.
    // A separate DI-registered instance handles runtime use with proper logging.
    private static IDataProtectionCertificateProvider RequireCertificateProvider(
        string? certProviderMode,
        string dpMode,
        IConfiguration configuration)
    {
        if (certProviderMode is null)
        {
            throw new InvalidOperationException(
                $"DataProtection:CertificateProvider must be configured when using '{dpMode}' key ring mode.");
        }

        if (string.Equals(certProviderMode, "FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            return new FileSystemCertificateProvider(
                configuration,
                NullLoggerFactory.Instance.CreateLogger<FileSystemCertificateProvider>());
        }

        if (string.Equals(certProviderMode, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            return new AzureBlobCertificateProvider(
                configuration,
                NullLoggerFactory.Instance.CreateLogger<AzureBlobCertificateProvider>());
        }

        // Unreachable: RegisterCertificateProvider already validated the mode.
        throw new InvalidOperationException(
            $"Unknown DataProtection:CertificateProvider mode: '{certProviderMode}'.");
    }
}