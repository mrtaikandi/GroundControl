using GroundControl.Api.Shared.Extensions.Options;
using GroundControl.Api.Shared.Security.DataProtection;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;
using DataProtectionOptions = GroundControl.Api.Shared.Security.DataProtection.DataProtectionOptions;

namespace GroundControl.Api.Shared.Security.KeyRing;

/// <summary>
/// Persists Data Protection keys to Redis.
/// Certificate-based key encryption is handled separately by
/// <see cref="Certificate.CertificateKeyEncryptionConfigurator"/>.
/// </summary>
internal sealed class RedisKeyRingConfigurator : IKeyRingConfigurator
{
    /// <inheritdoc />
    public void Configure(IDataProtectionBuilder builder, DataProtectionOptions options)
    {
        RedisOptions.Validator.ThrowIfInvalid(options.Redis);

        var connectionString = options.Redis.ConnectionString;
        var redisOptions = ConfigurationOptions.Parse(connectionString);

        redisOptions.ConnectTimeout = options.Redis.ConnectTimeoutMs;
        redisOptions.AbortOnConnectFail = true;

        IConnectionMultiplexer redis;
        try
        {
            redis = ConnectionMultiplexer.Connect(redisOptions);
        }
        catch (RedisConnectionException ex)
        {
            throw new InvalidOperationException($"Failed to connect to Redis for Data Protection key ring. Connection string: '{connectionString}'.", ex);
        }

        builder.PersistKeysToStackExchangeRedis(redis, options.Redis.KeyName);
    }
}