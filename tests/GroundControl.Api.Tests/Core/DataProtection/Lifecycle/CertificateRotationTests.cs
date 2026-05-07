using GroundControl.Api.Features.ConfigEntries.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle;

/// <summary>
/// Verifies that rotating the X.509 certificate that protects the Data Protection key ring
/// does not strand sensitive values written before the rotation. Factory A boots with cert
/// C1 as the current certificate; Factory B boots with C2 as current and C1 in
/// <c>DataProtection:FileSystemCertificate:PreviousPaths</c> so the key XML protected by C1
/// remains decryptable.
/// </summary>
public sealed class CertificateRotationTests : DataProtectionLifecycleTestBase
{
    private readonly string _keyStorePath;
    private readonly string _certificateDir;

    public CertificateRotationTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
        _keyStorePath = AllocateTempDirectory("gc-keys");
        _certificateDir = AllocateTempDirectory("gc-certs");
    }

    [Fact]
    public async Task PreRotationEntry_RemainsDecryptable_AfterCertificateSwap()
    {
        // Arrange — Cert C1 protects the key ring while Factory A creates a sensitive entry.
        var c1Path = SelfSignedCertificate.CreatePfxFile(_certificateDir, "c1.pfx");
        Guid preRotationId;

        await using (var factoryA = CreateLifecycleFactory(currentCertificatePath: c1Path, previousCertificatePaths: []))
        using (var clientA = factoryA.CreateClient())
        {
            var created = await CreateSensitiveConfigEntryAsync(clientA, "db.password", "before-cert-rotation");
            preRotationId = created.Id;

            Directory.GetFiles(_keyStorePath, "*.xml").ShouldNotBeEmpty(
                "key ring XML should have been written under C1 before the rotation");
        }

        // Act — Cert C2 is now current with C1 in the previous list; Factory B reads the entry.
        var c2Path = SelfSignedCertificate.CreatePfxFile(_certificateDir, "c2.pfx");
        await using var factoryB = CreateLifecycleFactory(currentCertificatePath: c2Path, previousCertificatePaths: [c1Path]);
        using var clientB = factoryB.CreateClient();

        var response = await clientB.GetAsync($"/api/config-entries/{preRotationId}?decrypt=true", TestCancellationToken);

        // Assert — entry written under C1 must still decrypt now that C2 is the current cert
        // and C1 has been moved to the previous list.
        response.IsSuccessStatusCode.ShouldBeTrue($"GET should succeed, but returned {response.StatusCode}.");
        var body = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        body.Values.ShouldHaveSingleItem().Value.ShouldBe("before-cert-rotation");
    }

    [Fact]
    public async Task PostRotationEntry_RoundTrips_UnderNewCertificateOnly()
    {
        // Arrange — Boot directly with C2 only; nothing was ever encrypted with C1.
        var c2Path = SelfSignedCertificate.CreatePfxFile(_certificateDir, "c2-only.pfx");

        // Act
        await using var factory = CreateLifecycleFactory(currentCertificatePath: c2Path, previousCertificatePaths: []);
        using var client = factory.CreateClient();

        var created = await CreateSensitiveConfigEntryAsync(client, "api.token", "after-cert-rotation");
        var response = await client.GetAsync($"/api/config-entries/{created.Id}?decrypt=true", TestCancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        var body = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        body.Values.ShouldHaveSingleItem().Value.ShouldBe("after-cert-rotation");
    }

    [Fact]
    public async Task PreRotationEntry_FailsToDecrypt_WhenOldCertificateNotInPreviousPaths()
    {
        // Arrange — C1 protects the key ring while Factory A writes a sensitive entry. Then
        // Factory B comes up with C2 ONLY (no previous list). Without the original certificate,
        // the key XML written under C1 cannot be unwrapped, and the protector cannot reconstruct
        // the AES key needed to decrypt the stored ciphertext. This guards against the "premature
        // old cert removal" risk listed in Security-Model.md.
        var c1Path = SelfSignedCertificate.CreatePfxFile(_certificateDir, "c1.pfx");
        Guid preRotationId;

        await using (var factoryA = CreateLifecycleFactory(currentCertificatePath: c1Path, previousCertificatePaths: []))
        using (var clientA = factoryA.CreateClient())
        {
            var created = await CreateSensitiveConfigEntryAsync(clientA, "db.password", "stranded-by-rotation");
            preRotationId = created.Id;
        }

        var c2Path = SelfSignedCertificate.CreatePfxFile(_certificateDir, "c2.pfx");

        // Act — Factory B drops C1 entirely; the pre-rotation entry should now be undecryptable.
        await using var factoryB = CreateLifecycleFactory(currentCertificatePath: c2Path, previousCertificatePaths: []);
        using var clientB = factoryB.CreateClient();

        var response = await clientB.GetAsync($"/api/config-entries/{preRotationId}?decrypt=true", TestCancellationToken);

        // Assert — surface as a server error rather than silent corruption / an empty response.
        response.IsSuccessStatusCode.ShouldBeFalse(
            $"Expected decrypt to fail without the original certificate, but server returned {response.StatusCode}.");
    }

    private GroundControlApiFactory CreateLifecycleFactory(string currentCertificatePath, IReadOnlyList<string> previousCertificatePaths)
    {
        var config = new Dictionary<string, string?>
        {
            ["Persistence:MongoDb:DatabaseName"] = DatabaseName,
            ["DataProtection:Mode"] = "Certificate",
            ["DataProtection:CertificateProvider"] = "FileSystem",
            ["DataProtection:FileSystemCertificate:Path"] = currentCertificatePath,
            ["DataProtection:KeyStorePath"] = _keyStorePath
        };

        for (var i = 0; i < previousCertificatePaths.Count; i++)
        {
            config[$"DataProtection:FileSystemCertificate:PreviousPaths:{i}"] = previousCertificatePaths[i];
        }

        return CreateFactory(config);
    }
}
