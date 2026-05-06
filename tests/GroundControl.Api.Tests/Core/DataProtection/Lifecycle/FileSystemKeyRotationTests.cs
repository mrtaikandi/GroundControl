using GroundControl.Api.Features.ConfigEntries.Contracts;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle;

/// <summary>
/// Verifies that adding a new Data Protection key to the ring does not render existing
/// sensitive values unreadable. The key id is embedded in the ciphertext, so old values
/// must continue to decrypt after a rotation; new values are protected with the new key.
/// </summary>
public sealed class FileSystemKeyRotationTests : DataProtectionLifecycleTestBase
{
    private readonly string _keyStorePath;

    public FileSystemKeyRotationTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
        _keyStorePath = AllocateTempDirectory("gc-keys");
    }

    [Fact]
    public async Task PreRotationEntry_RemainsDecryptable_AfterNewKeyAddedAndHostRestart()
    {
        Guid preRotationId;

        // Arrange — Factory A creates a sensitive entry under K1 then forces a rotation by
        // adding K2 to the ring with an activation date in the past so it is immediately eligible.
        await using (var factoryA = CreateLifecycleFactory())
        using (var clientA = factoryA.CreateClient())
        {
            var preRotation = await CreateSensitiveConfigEntryAsync(clientA, "db.password", "before-rotation");
            preRotationId = preRotation.Id;

            var keyCountBeforeRotation = Directory.GetFiles(_keyStorePath, "*.xml").Length;
            keyCountBeforeRotation.ShouldBeGreaterThanOrEqualTo(1);

            var keyManager = factoryA.Services.GetRequiredService<IKeyManager>();
            keyManager.CreateNewKey(
                activationDate: DateTimeOffset.UtcNow.AddHours(-1),
                expirationDate: DateTimeOffset.UtcNow.AddDays(90));

            Directory.GetFiles(_keyStorePath, "*.xml").Length.ShouldBeGreaterThan(
                keyCountBeforeRotation,
                "creating a new key should have written an additional key XML file");
        }

        // Act — Factory B is a fresh host pointed at the same key store. It should both
        // (a) decrypt the pre-rotation entry and (b) create new entries under the new key.
        await using var factoryB = CreateLifecycleFactory();
        using var clientB = factoryB.CreateClient();

        var preRotationResponse = await clientB.GetAsync($"/api/config-entries/{preRotationId}?decrypt=true", TestCancellationToken);
        var postRotation = await CreateSensitiveConfigEntryAsync(clientB, "api.token", "after-rotation");
        var postRotationResponse = await clientB.GetAsync($"/api/config-entries/{postRotation.Id}?decrypt=true", TestCancellationToken);

        // Assert — both entries decrypt to their original plaintext
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
        ["DataProtection:Mode"] = "FileSystem",
        ["DataProtection:KeyStorePath"] = _keyStorePath
    });
}
