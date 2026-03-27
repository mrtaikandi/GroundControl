using GroundControl.Api.Shared.Security.Certificate;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

namespace GroundControl.Api.Shared.Security.KeyRing;

/// <summary>
/// Persists Data Protection keys to Redis and protects them with an X.509 certificate.
/// </summary>
internal sealed class RedisKeyRingConfigurator(IDataProtectionCertificateProvider certificateProvider) : IKeyRingConfigurator
{
    private const string DefaultKeyName = "groundcontrol-data-protection";
    private const int DefaultConnectTimeoutMs = 5000;

    /// <inheritdoc />
    public void Configure(IDataProtectionBuilder builder, IConfiguration configuration)
    {
        var connectionString = configuration["DataProtection:Redis:ConnectionString"]
            ?? throw new InvalidOperationException("DataProtection:Redis:ConnectionString is required for Redis mode.");

        var keyName = configuration["DataProtection:Redis:KeyName"] ?? DefaultKeyName;

        var options = ConfigurationOptions.Parse(connectionString);
        options.ConnectTimeout = configuration.GetValue("DataProtection:Redis:ConnectTimeoutMs", DefaultConnectTimeoutMs);
        options.AbortOnConnectFail = true;

        IConnectionMultiplexer redis;
        try
        {
            redis = ConnectionMultiplexer.Connect(options);
        }
        catch (RedisConnectionException ex)
        {
            throw new InvalidOperationException(
                $"Failed to connect to Redis for Data Protection key ring. Connection string: '{connectionString}'.", ex);
        }

        // Sync-over-async: safe here because this runs in the startup path with no SynchronizationContext.
        var certificate = certificateProvider.GetCurrentCertificateAsync()
            .GetAwaiter().GetResult();

        builder
            .PersistKeysToStackExchangeRedis(redis, keyName)
            .ProtectKeysWithCertificate(certificate);

        foreach (var previous in certificateProvider.GetPreviousCertificatesAsync().GetAwaiter().GetResult())
        {
            builder.UnprotectKeysWithAnyCertificate(previous);
        }
    }
}