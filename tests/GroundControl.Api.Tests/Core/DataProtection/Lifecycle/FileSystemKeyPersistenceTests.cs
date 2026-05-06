using GroundControl.Api.Features.ConfigEntries.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle;

/// <summary>
/// Verifies that sensitive configuration values written under one API host instance
/// remain decryptable after the host is disposed and a fresh host is started against
/// the same on-disk key store and MongoDB database. This is the foundational guarantee
/// for any deployment that survives a process restart.
/// </summary>
public sealed class FileSystemKeyPersistenceTests : DataProtectionLifecycleTestBase
{
    private readonly string _keyStorePath;

    public FileSystemKeyPersistenceTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
        _keyStorePath = AllocateTempDirectory("gc-keys");
    }

    [Fact]
    public async Task SensitiveValue_RemainsDecryptable_AfterApiHostRestart()
    {
        // Arrange — Factory A creates a sensitive entry under the shared key store + DB
        Guid createdId;

        await using (var factoryA = CreateLifecycleFactory())
        using (var clientA = factoryA.CreateClient())
        {
            var created = await CreateSensitiveConfigEntryAsync(clientA, "db.password", "s3cret!");
            createdId = created.Id;

            Directory.GetFiles(_keyStorePath, "*.xml").ShouldNotBeEmpty(
                "FileSystem mode should have persisted at least one key XML file before restart");
        }

        // Act — Factory B is a fresh host pointed at the same key store + DB
        await using var factoryB = CreateLifecycleFactory();
        using var clientB = factoryB.CreateClient();

        var response = await clientB.GetAsync($"/api/config-entries/{createdId}?decrypt=true", TestCancellationToken);

        // Assert — Factory B reads back the plaintext written under Factory A
        response.IsSuccessStatusCode.ShouldBeTrue();
        var body = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        body.IsSensitive.ShouldBeTrue();
        body.Values.ShouldHaveSingleItem().Value.ShouldBe("s3cret!");
    }

    private GroundControlApiFactory CreateLifecycleFactory() => CreateFactory(new Dictionary<string, string?>
    {
        ["Persistence:MongoDb:DatabaseName"] = DatabaseName,
        ["DataProtection:Mode"] = "FileSystem",
        ["DataProtection:KeyStorePath"] = _keyStorePath
    });
}
