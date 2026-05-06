using System.Net.Http.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle;

/// <summary>
/// Shared infrastructure for Data Protection lifecycle integration tests. Provides a stable
/// MongoDB database name (so two factory instantiations can simulate a host restart against
/// the same persisted state), a small helper to allocate temp directories with deferred
/// cleanup, and a helper for the most common HTTP shape these tests perform — creating a
/// sensitive config entry and parsing the response.
/// </summary>
public abstract class DataProtectionLifecycleTestBase : ApiHandlerTestBase, IDisposable
{
    private readonly List<string> _tempDirectories = [];

    protected DataProtectionLifecycleTestBase(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    /// <summary>
    /// Database name reused by every factory the test creates so a "second host" sees the
    /// MongoDB state written by the first.
    /// </summary>
    protected string DatabaseName { get; } = $"groundcontrol_test_{Guid.CreateVersion7():N}";

    /// <summary>
    /// Returns a fresh path under the system temp directory and registers it for cleanup
    /// when the test class is disposed. The directory itself is created lazily by the code
    /// that writes to it (e.g. ASP.NET Data Protection key persistence, certificate file writes).
    /// </summary>
    protected string AllocateTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        _tempDirectories.Add(path);
        return path;
    }

    /// <summary>
    /// POSTs a sensitive <see cref="ConfigEntry"/> and returns the parsed response. The owning
    /// project id is generated per call because these tests do not need a real owner — they
    /// only exercise persistence and decryption of the sensitive value.
    /// </summary>
    internal static async Task<ConfigEntryResponse> CreateSensitiveConfigEntryAsync(HttpClient client, string key, string value)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = Guid.CreateVersion7(),
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = value }],
            IsSensitive = true
        };

        var response = await client.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        foreach (var path in _tempDirectories)
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
                // Disk locks (AV scans, lingering file handles) should not fail an otherwise green test.
            }
        }
    }
}
