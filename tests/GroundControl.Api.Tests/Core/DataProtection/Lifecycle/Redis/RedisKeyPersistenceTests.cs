using GroundControl.Api.Features.ConfigEntries.Contracts;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle.Redis;

/// <summary>
/// Verifies that the Redis-backed Data Protection key ring lets a freshly started API host
/// decrypt sensitive values that were written under a previous host instance, both for the
/// horizontally-scaled deployment scenario (two instances sharing one Redis) and the simple
/// "process restart" scenario.
/// </summary>
public sealed class RedisKeyPersistenceTests : DataProtectionLifecycleTestBase
{
    private readonly RedisFixture _redisFixture;
    private readonly string _redisKeyName = $"groundcontrol-test-keys-{Guid.NewGuid():N}";
    private readonly string _certificatePath;

    public RedisKeyPersistenceTests(MongoFixture mongoFixture, RedisFixture redisFixture)
        : base(mongoFixture)
    {
        _redisFixture = redisFixture;
        _certificatePath = SelfSignedCertificate.CreatePfxFile(AllocateTempDirectory("gc-certs"), "dp.pfx", password: null);
    }

    [Fact]
    public async Task SensitiveValue_RemainsDecryptable_AfterApiHostRestart()
    {
        // Arrange — Factory A protects a value under the Redis-backed key ring.
        Guid createdId;

        await using (var factoryA = CreateLifecycleFactory())
        using (var clientA = factoryA.CreateClient())
        {
            var created = await CreateSensitiveConfigEntryAsync(clientA, "db.password", "redis-secret");
            createdId = created.Id;

            // Sanity — at least one key XML must now live in Redis under the configured key name.
            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
            (await multiplexer.GetDatabase().ListLengthAsync(_redisKeyName))
                .ShouldBeGreaterThan(0, $"Redis list '{_redisKeyName}' should hold at least one key XML entry");
        }

        // Act — Factory B is a fresh host pointed at the same Redis + Mongo + cert.
        await using var factoryB = CreateLifecycleFactory();
        using var clientB = factoryB.CreateClient();

        var response = await clientB.GetAsync($"/api/config-entries/{createdId}?decrypt=true", TestCancellationToken);

        // Assert — Factory B reads back the plaintext written under Factory A.
        response.IsSuccessStatusCode.ShouldBeTrue();
        var body = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        body.IsSensitive.ShouldBeTrue();
        body.Values.ShouldHaveSingleItem().Value.ShouldBe("redis-secret");
    }

    private GroundControlApiFactory CreateLifecycleFactory() => CreateFactory(new Dictionary<string, string?>
    {
        ["Persistence:MongoDb:DatabaseName"] = DatabaseName,
        ["DataProtection:Mode"] = "Redis",
        ["DataProtection:CertificateProvider"] = "FileSystem",
        ["DataProtection:CertificatePath"] = _certificatePath,
        ["DataProtection:Redis:ConnectionString"] = _redisFixture.ConnectionString,
        ["DataProtection:Redis:KeyName"] = _redisKeyName
    });
}
