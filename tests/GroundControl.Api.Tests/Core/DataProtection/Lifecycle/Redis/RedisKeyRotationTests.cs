using GroundControl.Api.Features.ConfigEntries.Contracts;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle.Redis;

/// <summary>
/// Redis equivalent of the FileSystem key-rotation test. Adding a new Data Protection key
/// to a Redis-backed ring must not strand existing sensitive values; the key id is embedded
/// in the ciphertext, so old values continue to decrypt while new values are protected with
/// the freshly added key.
/// </summary>
public sealed class RedisKeyRotationTests : DataProtectionLifecycleTestBase
{
    private readonly RedisFixture _redisFixture;
    private readonly string _redisKeyName = $"groundcontrol-test-keys-{Guid.NewGuid():N}";
    private readonly string _certificatePath;

    public RedisKeyRotationTests(MongoFixture mongoFixture, RedisFixture redisFixture)
        : base(mongoFixture)
    {
        _redisFixture = redisFixture;
        _certificatePath = SelfSignedCertificate.CreatePfxFile(AllocateTempDirectory("gc-certs"), "dp.pfx", password: null);
    }

    [Fact]
    public async Task PreRotationEntry_RemainsDecryptable_AfterNewKeyAddedAndHostRestart()
    {
        Guid preRotationId;

        // Arrange — Factory A creates a sensitive entry under K1 then forces a rotation by
        // adding K2 with an activation date in the past so the next host picks it up immediately.
        await using (var factoryA = CreateLifecycleFactory())
        using (var clientA = factoryA.CreateClient())
        {
            var preRotation = await CreateSensitiveConfigEntryAsync(clientA, "db.password", "before-rotation");
            preRotationId = preRotation.Id;

            await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
            var keyCountBefore = await multiplexer.GetDatabase().ListLengthAsync(_redisKeyName);
            keyCountBefore.ShouldBeGreaterThanOrEqualTo(1);

            var keyManager = factoryA.Services.GetRequiredService<IKeyManager>();
            keyManager.CreateNewKey(
                activationDate: DateTimeOffset.UtcNow.AddHours(-1),
                expirationDate: DateTimeOffset.UtcNow.AddDays(90));

            (await multiplexer.GetDatabase().ListLengthAsync(_redisKeyName))
                .ShouldBeGreaterThan(keyCountBefore, "creating a new key should append a new entry to Redis");
        }

        // Act — Factory B is a fresh host pointed at the same Redis instance.
        await using var factoryB = CreateLifecycleFactory();
        using var clientB = factoryB.CreateClient();

        var preRotationResponse = await clientB.GetAsync($"/api/config-entries/{preRotationId}?decrypt=true", TestCancellationToken);
        var postRotation = await CreateSensitiveConfigEntryAsync(clientB, "api.token", "after-rotation");
        var postRotationResponse = await clientB.GetAsync($"/api/config-entries/{postRotation.Id}?decrypt=true", TestCancellationToken);

        // Assert — both pre- and post-rotation entries decrypt correctly under Factory B.
        preRotationResponse.IsSuccessStatusCode.ShouldBeTrue();
        var preBody = await ReadRequiredJsonAsync<ConfigEntryResponse>(preRotationResponse, TestCancellationToken);
        preBody.Values.ShouldHaveSingleItem().Value.ShouldBe("before-rotation");

        postRotationResponse.IsSuccessStatusCode.ShouldBeTrue();
        var postBody = await ReadRequiredJsonAsync<ConfigEntryResponse>(postRotationResponse, TestCancellationToken);
        postBody.Values.ShouldHaveSingleItem().Value.ShouldBe("after-rotation");
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
