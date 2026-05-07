using GroundControl.Api.Features.ConfigEntries.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle.Redis;

/// <summary>
/// Redis equivalent of <see cref="FileSystemScaledOutTests"/>: two API hosts running concurrently
/// against the same Redis-backed Data Protection key ring must be able to decrypt each other's
/// sensitive values without restart. This is the canonical multi-instance scenario for Redis mode.
/// </summary>
public sealed class RedisScaledOutTests : DataProtectionLifecycleTestBase
{
    private readonly RedisFixture _redisFixture;
    private readonly string _redisKeyName = $"groundcontrol-test-keys-{Guid.NewGuid():N}";
    private readonly string _certificatePath;

    public RedisScaledOutTests(MongoFixture mongoFixture, RedisFixture redisFixture)
        : base(mongoFixture)
    {
        _redisFixture = redisFixture;
        _certificatePath = SelfSignedCertificate.CreatePfxFile(AllocateTempDirectory("gc-certs"), "dp.pfx", password: null);
    }

    [Fact]
    public async Task SensitiveValue_DecryptableByLivePeer_SharingRedisKeyRing()
    {
        // Arrange
        await using var factoryA = CreateLifecycleFactory();
        await using var factoryB = CreateLifecycleFactory();

        using var clientA = factoryA.CreateClient();
        using var clientB = factoryB.CreateClient();

        // Act — A protects, B reads back without restart.
        var created = await CreateSensitiveConfigEntryAsync(clientA, "db.password", "redis-live-peer");
        var response = await clientB.GetAsync($"/api/config-entries/{created.Id}?decrypt=true", TestCancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        var body = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        body.IsSensitive.ShouldBeTrue();
        body.Values.ShouldHaveSingleItem().Value.ShouldBe("redis-live-peer");
    }

    private GroundControlApiFactory CreateLifecycleFactory() => CreateFactory(new Dictionary<string, string?>
    {
        ["Persistence:MongoDb:DatabaseName"] = DatabaseName,
        ["DataProtection:Mode"] = "Redis",
        ["DataProtection:CertificateProvider"] = "FileSystem",
        ["DataProtection:FileSystemCertificate:Path"] = _certificatePath,
        ["DataProtection:Redis:ConnectionString"] = _redisFixture.ConnectionString,
        ["DataProtection:Redis:KeyName"] = _redisKeyName
    });
}
