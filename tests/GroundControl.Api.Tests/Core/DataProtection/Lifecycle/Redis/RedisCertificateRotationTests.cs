using GroundControl.Api.Features.ConfigEntries.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle.Redis;

/// <summary>
/// Redis equivalent of <see cref="CertificateRotationTests"/>: rotating the X.509 certificate
/// that protects a Redis-backed key ring must not strand sensitive values written before the
/// rotation. Mode=Redis shares the certificate-protection wiring with Mode=Certificate, so
/// this test guards against regressions on either mode.
/// </summary>
public sealed class RedisCertificateRotationTests : DataProtectionLifecycleTestBase
{
    private readonly RedisFixture _redisFixture;
    private readonly string _redisKeyName = $"groundcontrol-test-keys-{Guid.NewGuid():N}";
    private readonly string _certificateDir;

    public RedisCertificateRotationTests(MongoFixture mongoFixture, RedisFixture redisFixture)
        : base(mongoFixture)
    {
        _redisFixture = redisFixture;
        _certificateDir = AllocateTempDirectory("gc-certs");
    }

    [Fact]
    public async Task PreRotationEntry_RemainsDecryptable_AfterCertificateSwap()
    {
        // Arrange — Cert C1 protects the Redis-backed key ring while Factory A writes a
        // sensitive entry; the resulting key XML lives in Redis encrypted with C1.
        var c1Path = SelfSignedCertificate.CreatePfxFile(_certificateDir, "c1.pfx");
        Guid preRotationId;

        await using (var factoryA = CreateLifecycleFactory(currentCertificatePath: c1Path, previousCertificatePaths: []))
        {
            using var clientA = factoryA.CreateClient();
            var created = await CreateSensitiveConfigEntryAsync(clientA, "db.password", "before-cert-rotation");
            preRotationId = created.Id;
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

    private GroundControlApiFactory CreateLifecycleFactory(string currentCertificatePath, IReadOnlyList<string> previousCertificatePaths)
    {
        var config = new Dictionary<string, string?>
        {
            ["Persistence:MongoDb:DatabaseName"] = DatabaseName,
            ["DataProtection:Mode"] = "Redis",
            ["DataProtection:CertificateProvider"] = "FileSystem",
            ["DataProtection:FileSystemCertificate:Path"] = currentCertificatePath,
            ["DataProtection:Redis:ConnectionString"] = _redisFixture.ConnectionString,
            ["DataProtection:Redis:KeyName"] = _redisKeyName
        };

        for (var i = 0; i < previousCertificatePaths.Count; i++)
        {
            config[$"DataProtection:FileSystemCertificate:PreviousPaths:{i}"] = previousCertificatePaths[i];
        }

        return CreateFactory(config);
    }
}
