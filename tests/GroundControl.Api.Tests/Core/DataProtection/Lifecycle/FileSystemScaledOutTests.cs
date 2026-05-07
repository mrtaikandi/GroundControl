using GroundControl.Api.Features.ConfigEntries.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle;

/// <summary>
/// Verifies that two API hosts running concurrently against the same FileSystem-backed key
/// store can decrypt each other's sensitive values without restart. This is the multi-instance
/// deployment scenario (e.g. a horizontally-scaled service): the per-restart tests prove a host
/// can read state written by its predecessor; this test proves a peer can read it live.
/// </summary>
public sealed class FileSystemScaledOutTests : DataProtectionLifecycleTestBase
{
    private readonly string _keyStorePath;

    public FileSystemScaledOutTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
        _keyStorePath = AllocateTempDirectory("gc-keys");
    }

    [Fact]
    public async Task SensitiveValue_DecryptableByLivePeer_SharingKeyStoreAndDatabase()
    {
        // Arrange — two factories alive at the same time, sharing the same key store + DB.
        await using var factoryA = CreateLifecycleFactory();
        await using var factoryB = CreateLifecycleFactory();

        using var clientA = factoryA.CreateClient();
        using var clientB = factoryB.CreateClient();

        // Act — A protects, then B reads back without anyone restarting.
        var created = await CreateSensitiveConfigEntryAsync(clientA, "db.password", "live-peer-secret");
        var response = await clientB.GetAsync($"/api/config-entries/{created.Id}?decrypt=true", TestCancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        var body = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        body.IsSensitive.ShouldBeTrue();
        body.Values.ShouldHaveSingleItem().Value.ShouldBe("live-peer-secret");
    }

    [Fact]
    public async Task SensitiveValue_DecryptableInBothDirections_AcrossLiveHosts()
    {
        // Arrange
        await using var factoryA = CreateLifecycleFactory();
        await using var factoryB = CreateLifecycleFactory();

        using var clientA = factoryA.CreateClient();
        using var clientB = factoryB.CreateClient();

        // Act — Each host writes a value; each host then reads back the other's.
        var fromA = await CreateSensitiveConfigEntryAsync(clientA, "db.password", "secret-from-a");
        var fromB = await CreateSensitiveConfigEntryAsync(clientB, "api.token", "secret-from-b");

        var bReadsA = await clientB.GetAsync($"/api/config-entries/{fromA.Id}?decrypt=true", TestCancellationToken);
        var aReadsB = await clientA.GetAsync($"/api/config-entries/{fromB.Id}?decrypt=true", TestCancellationToken);

        // Assert
        bReadsA.IsSuccessStatusCode.ShouldBeTrue();
        var bBody = await ReadRequiredJsonAsync<ConfigEntryResponse>(bReadsA, TestCancellationToken);
        bBody.Values.ShouldHaveSingleItem().Value.ShouldBe("secret-from-a");

        aReadsB.IsSuccessStatusCode.ShouldBeTrue();
        var aBody = await ReadRequiredJsonAsync<ConfigEntryResponse>(aReadsB, TestCancellationToken);
        aBody.Values.ShouldHaveSingleItem().Value.ShouldBe("secret-from-b");
    }

    private GroundControlApiFactory CreateLifecycleFactory() => CreateFactory(new Dictionary<string, string?>
    {
        ["Persistence:MongoDb:DatabaseName"] = DatabaseName,
        ["DataProtection:Mode"] = "FileSystem",
        ["DataProtection:KeyStorePath"] = _keyStorePath
    });
}
