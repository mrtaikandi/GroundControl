using System.Text;

namespace GroundControl.Link.Tests.Cache;

public sealed class FileConfigurationCacheTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly string _cachePath;

    public FileConfigurationCacheTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "groundcontrol-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheDir);
        _cachePath = Path.Combine(_cacheDir, "test.cache.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsNull()
    {
        // Arrange
        using var cache = CreateCache();

        // Act
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_CorruptedJson_ReturnsNull()
    {
        // Arrange
        await File.WriteAllTextAsync(_cachePath, "not valid json {{{", TestContext.Current.CancellationToken);
        using var cache = CreateCache();

        // Act
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_EmptyEntriesObject_ReturnsNull()
    {
        // Arrange
        await File.WriteAllTextAsync(_cachePath, """{"timestamp":"2026-01-01T00:00:00Z","protected":false,"entries":null}""", TestContext.Current.CancellationToken);
        using var cache = CreateCache();

        // Act
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ReturnsSameConfig()
    {
        // Arrange
        using var cache = CreateCache();
        var config = Dict(
            ("Logging:LogLevel:Default", "Warning"),
            ("Database:Host", "localhost"),
            ("FeatureFlags:DarkMode", "true"));

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Entries.Count.ShouldBe(3);
        result.Entries["Logging:LogLevel:Default"].Value.ShouldBe("Warning");
        result.Entries["Database:Host"].Value.ShouldBe("localhost");
        result.Entries["FeatureFlags:DarkMode"].Value.ShouldBe("true");
    }

    [Fact]
    public async Task SaveAsync_CreatesFile()
    {
        // Arrange
        using var cache = CreateCache();
        var config = Dict(("Key1", "Value1"));

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(_cachePath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var nestedDir = Path.Combine(_cacheDir, "nested", "deep");
        var nestedPath = Path.Combine(nestedDir, "cache.json");
        using var cache = CreateCache(cachePath: nestedPath);
        var config = Dict(("Key1", "Value1"));

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(nestedPath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFile()
    {
        // Arrange
        using var cache = CreateCache();
        var config1 = Dict(("Key1", "Original"));
        var config2 = Dict(("Key1", "Updated"), ("Key2", "New"));

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config1 }, TestContext.Current.CancellationToken);
        await cache.SaveAsync(new CachedConfiguration { Entries = config2 }, TestContext.Current.CancellationToken);
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Entries.Count.ShouldBe(2);
        result.Entries["Key1"].Value.ShouldBe("Updated");
        result.Entries["Key2"].Value.ShouldBe("New");
    }

    [Fact]
    public async Task SaveAsync_WithProtector_EncryptsOnlySensitiveValuesInFile()
    {
        // Arrange
        using var cache = CreateCache(protector: new XorProtector());
        var entries = new Dictionary<string, ConfigValue>
        {
            ["Secret"] = V("my-password", isSensitive: true),
            ["PublicUrl"] = V("https://example.com")
        };

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = entries }, TestContext.Current.CancellationToken);

        // Assert — sensitive value is encrypted, non-sensitive value stays plaintext
        var rawJson = await File.ReadAllTextAsync(_cachePath, TestContext.Current.CancellationToken);
        rawJson.ShouldNotContain("my-password");
        rawJson.ShouldContain("https://example.com");
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_WithProtector_DecryptsSensitiveValues()
    {
        // Arrange
        using var cache = CreateCache(protector: new XorProtector());
        var entries = new Dictionary<string, ConfigValue>
        {
            ["Secret"] = V("my-password", isSensitive: true),
            ["ApiKey"] = V("abc-123", isSensitive: true),
            ["Feature:Flag"] = V("enabled")
        };

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = entries }, TestContext.Current.CancellationToken);
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Entries["Secret"].Value.ShouldBe("my-password");
        result.Entries["Secret"].IsSensitive.ShouldBeTrue();
        result.Entries["ApiKey"].Value.ShouldBe("abc-123");
        result.Entries["ApiKey"].IsSensitive.ShouldBeTrue();
        result.Entries["Feature:Flag"].Value.ShouldBe("enabled");
        result.Entries["Feature:Flag"].IsSensitive.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_WithoutProtector_StoresEverythingPlaintext()
    {
        // Arrange — no protector configured, even sensitive entries land on disk in plaintext per the opt-out policy
        using var cache = CreateCache();
        var entries = new Dictionary<string, ConfigValue>
        {
            ["Key1"] = V("plaintext-value"),
            ["Secret"] = V("opt-out-secret", isSensitive: true)
        };

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = entries }, TestContext.Current.CancellationToken);

        // Assert
        var rawJson = await File.ReadAllTextAsync(_cachePath, TestContext.Current.CancellationToken);
        rawJson.ShouldContain("plaintext-value");
        rawJson.ShouldContain("opt-out-secret");
    }

    [Fact]
    public async Task LoadAsync_ProtectedEnvelopeWithoutProtector_ReturnsNull()
    {
        // Arrange — write with a protector, read without one
        using var encryptedCache = CreateCache(protector: new XorProtector());
        var entries = new Dictionary<string, ConfigValue> { ["Secret"] = V("my-password", isSensitive: true) };
        await encryptedCache.SaveAsync(new CachedConfiguration { Entries = entries }, TestContext.Current.CancellationToken);

        // Act
        using var plainCache = CreateCache();
        var result = await plainCache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_UnprotectedEnvelopeWithProtector_ReturnsNull()
    {
        // Arrange — write without protector, read with one (prevents silent downgrade / plaintext fed to Unprotect)
        using var plainCache = CreateCache();
        var entries = new Dictionary<string, ConfigValue> { ["Key1"] = V("value") };
        await plainCache.SaveAsync(new CachedConfiguration { Entries = entries }, TestContext.Current.CancellationToken);

        // Act
        using var encryptedCache = CreateCache(protector: new XorProtector());
        var result = await encryptedCache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_UnprotectThrows_ReturnsNull()
    {
        // Arrange — write with one protector, read with another that rejects the ciphertext
        using var writeCache = CreateCache(protector: new XorProtector());
        var entries = new Dictionary<string, ConfigValue> { ["Secret"] = V("my-password", isSensitive: true) };
        await writeCache.SaveAsync(new CachedConfiguration { Entries = entries }, TestContext.Current.CancellationToken);

        using var readCache = CreateCache(protector: new ThrowingProtector());

        // Act
        var result = await readCache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_MixedEntries_NonSensitiveStayPlaintextSensitiveDecrypt()
    {
        // Arrange
        using var cache = CreateCache(protector: new XorProtector());
        var entries = new Dictionary<string, ConfigValue>
        {
            ["Db:Host"] = V("db.example.com"),
            ["Db:Password"] = V("hunter2", isSensitive: true),
            ["Logging:Level"] = V("Information")
        };

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = entries }, TestContext.Current.CancellationToken);

        var rawJson = await File.ReadAllTextAsync(_cachePath, TestContext.Current.CancellationToken);
        var loaded = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert — on disk: non-sensitive values are inspectable, sensitive values are not
        rawJson.ShouldContain("db.example.com");
        rawJson.ShouldContain("Information");
        rawJson.ShouldNotContain("hunter2");

        loaded.ShouldNotBeNull();
        loaded.Entries["Db:Host"].Value.ShouldBe("db.example.com");
        loaded.Entries["Db:Host"].IsSensitive.ShouldBeFalse();
        loaded.Entries["Db:Password"].Value.ShouldBe("hunter2");
        loaded.Entries["Db:Password"].IsSensitive.ShouldBeTrue();
        loaded.Entries["Logging:Level"].Value.ShouldBe("Information");
    }

    [Fact]
    public async Task CacheFilePath_FromOptions_IsRespected()
    {
        // Arrange
        var customPath = Path.Combine(_cacheDir, "custom", "path.json");
        using var cache = CreateCache(cachePath: customPath);
        var config = Dict(("Key1", "Value1"));

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(customPath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_ConcurrentWrites_NoCorruption()
    {
        // Arrange
        using var cache = CreateCache();
        var tasks = new List<Task>();

        // Act — fire multiple concurrent writes
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(cache.SaveAsync(
                new CachedConfiguration { Entries = Dict(("Key", $"Value{index}")) },
                TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks);

        // Assert — the file should be readable and contain a valid config
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Entries.ShouldContainKey("Key");
        result.Entries["Key"].Value.ShouldStartWith("Value");
    }

    [Fact]
    public async Task SaveAsync_CacheFileContainsTimestamp()
    {
        // Arrange
        using var cache = CreateCache();
        var config = Dict(("Key1", "Value1"));

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        // Assert
        var rawJson = await File.ReadAllTextAsync(_cachePath, TestContext.Current.CancellationToken);
        rawJson.ShouldContain("timestamp");
    }

    [Fact]
    public async Task LoadAsync_CaseInsensitiveKeys()
    {
        // Arrange
        using var cache = CreateCache();
        var config = Dict(("MyKey", "Value"));
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        // Act
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Entries["mykey"].Value.ShouldBe("Value");
        result.Entries["MYKEY"].Value.ShouldBe("Value");
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        // Arrange
        using var cache = CreateCache();

        // Act
        var result = cache.Load();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Save_ThenLoad_ReturnsSameConfig()
    {
        // Arrange
        using var cache = CreateCache();
        var config = new CachedConfiguration
        {
            Entries = Dict(("App:Name", "Test")),
            ETag = "\"42\"",
            LastEventId = "evt-1"
        };

        // Act
        cache.Save(config);
        var loaded = cache.Load();

        // Assert
        loaded.ShouldNotBeNull();
        loaded.Entries["App:Name"].Value.ShouldBe("Test");
        loaded.ETag.ShouldBe("\"42\"");
        loaded.LastEventId.ShouldBe("evt-1");
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsNull()
    {
        // Arrange
        File.WriteAllText(_cachePath, "not-valid-json{{{");
        using var cache = CreateCache();

        // Act
        var result = cache.Load();

        // Assert
        result.ShouldBeNull();
    }

    private FileConfigurationCache CreateCache(string? cachePath = null, IConfigurationProtector? protector = null) =>
        new(
            new GroundControlOptions
            {
                ServerUrl = new Uri("http://localhost"),
                ClientId = "test-client",
                ClientSecret = "test-secret",
                CacheFilePath = cachePath ?? _cachePath,
                Protector = protector
            },
            NullLogger<FileConfigurationCache>.Instance);

    private sealed class XorProtector : IConfigurationProtector
    {
        public string Protect(string plaintext) => Convert.ToBase64String(Xor(Encoding.UTF8.GetBytes(plaintext)));

        public string Unprotect(string ciphertext) => Encoding.UTF8.GetString(Xor(Convert.FromBase64String(ciphertext)));

        private static byte[] Xor(byte[] input)
        {
            var output = new byte[input.Length];
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (byte)(input[i] ^ 0xFF);
            }

            return output;
        }
    }

    private sealed class ThrowingProtector : IConfigurationProtector
    {
        public string Protect(string plaintext) => plaintext;

        public string Unprotect(string ciphertext) => throw new InvalidOperationException("decryption failed");
    }
}