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
        await File.WriteAllTextAsync(_cachePath, """{"timestamp":"2026-01-01T00:00:00Z","entries":null}""", TestContext.Current.CancellationToken);
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
        var config = new Dictionary<string, string>
        {
            ["Logging:LogLevel:Default"] = "Warning",
            ["Database:Host"] = "localhost",
            ["FeatureFlags:DarkMode"] = "true"
        };

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Entries.Count.ShouldBe(3);
        result.Entries["Logging:LogLevel:Default"].ShouldBe("Warning");
        result.Entries["Database:Host"].ShouldBe("localhost");
        result.Entries["FeatureFlags:DarkMode"].ShouldBe("true");
    }

    [Fact]
    public async Task SaveAsync_CreatesFile()
    {
        // Arrange
        using var cache = CreateCache();
        var config = new Dictionary<string, string> { ["Key1"] = "Value1" };

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
        var config = new Dictionary<string, string> { ["Key1"] = "Value1" };

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
        var config1 = new Dictionary<string, string> { ["Key1"] = "Original" };
        var config2 = new Dictionary<string, string> { ["Key1"] = "Updated", ["Key2"] = "New" };

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config1 }, TestContext.Current.CancellationToken);
        await cache.SaveAsync(new CachedConfiguration { Entries = config2 }, TestContext.Current.CancellationToken);
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Entries.Count.ShouldBe(2);
        result.Entries["Key1"].ShouldBe("Updated");
        result.Entries["Key2"].ShouldBe("New");
    }

    [Fact]
    public async Task SaveAsync_WithProtector_EncryptsValuesInFile()
    {
        // Arrange
        using var cache = CreateCache(protector: new XorProtector());
        var config = new Dictionary<string, string> { ["Secret"] = "my-password" };

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        // Assert
        var rawJson = await File.ReadAllTextAsync(_cachePath, TestContext.Current.CancellationToken);
        rawJson.ShouldContain("***ENCRYPTED:");
        rawJson.ShouldNotContain("my-password");
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_WithProtector_DecryptsValues()
    {
        // Arrange
        using var cache = CreateCache(protector: new XorProtector());
        var config = new Dictionary<string, string>
        {
            ["Secret"] = "my-password",
            ["ApiKey"] = "abc-123"
        };

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Entries["Secret"].ShouldBe("my-password");
        result.Entries["ApiKey"].ShouldBe("abc-123");
    }

    [Fact]
    public async Task SaveAsync_WithoutProtector_StoresPlaintext()
    {
        // Arrange
        using var cache = CreateCache();
        var config = new Dictionary<string, string> { ["Key1"] = "plaintext-value" };

        // Act
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        // Assert
        var rawJson = await File.ReadAllTextAsync(_cachePath, TestContext.Current.CancellationToken);
        rawJson.ShouldContain("plaintext-value");
        rawJson.ShouldNotContain("***ENCRYPTED:");
    }

    [Fact]
    public async Task LoadAsync_EncryptedValuesWithoutProtector_InvalidatesCacheAndReturnsNull()
    {
        // Arrange — write encrypted values using a protector
        using var encryptedCache = CreateCache(protector: new XorProtector());
        var config = new Dictionary<string, string> { ["Secret"] = "my-password" };
        await encryptedCache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        // Act — read without a protector
        using var plainCache = CreateCache();
        var result = await plainCache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
        File.Exists(_cachePath).ShouldBeFalse();
    }

    [Fact]
    public async Task LoadAsync_PlaintextValuesWithProtector_InvalidatesCacheAndReturnsNull()
    {
        // Arrange — write plaintext values
        using var plainCache = CreateCache();
        var config = new Dictionary<string, string> { ["Key1"] = "value" };
        await plainCache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        // Act — read with a protector configured (downgrade prevention)
        using var encryptedCache = CreateCache(protector: new XorProtector());
        var result = await encryptedCache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
        File.Exists(_cachePath).ShouldBeFalse();
    }

    [Fact]
    public async Task LoadAsync_UnprotectThrows_InvalidatesCacheAndReturnsNull()
    {
        // Arrange — write with one protector, read with another that rejects the ciphertext
        using var writeCache = CreateCache(protector: new XorProtector());
        var config = new Dictionary<string, string> { ["Secret"] = "my-password" };
        await writeCache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        using var readCache = CreateCache(protector: new ThrowingProtector());

        // Act
        var result = await readCache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
        File.Exists(_cachePath).ShouldBeFalse();
    }

    [Fact]
    public async Task CacheFilePath_FromOptions_IsRespected()
    {
        // Arrange
        var customPath = Path.Combine(_cacheDir, "custom", "path.json");
        using var cache = CreateCache(cachePath: customPath);
        var config = new Dictionary<string, string> { ["Key1"] = "Value1" };

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
                new CachedConfiguration { Entries = new Dictionary<string, string> { ["Key"] = $"Value{index}" } },
                TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks);

        // Assert — the file should be readable and contain a valid config
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Entries.ShouldContainKey("Key");
        result.Entries["Key"].ShouldStartWith("Value");
    }

    [Fact]
    public async Task SaveAsync_CacheFileContainsTimestamp()
    {
        // Arrange
        using var cache = CreateCache();
        var config = new Dictionary<string, string> { ["Key1"] = "Value1" };

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
        var config = new Dictionary<string, string> { ["MyKey"] = "Value" };
        await cache.SaveAsync(new CachedConfiguration { Entries = config }, TestContext.Current.CancellationToken);

        // Act
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Entries["mykey"].ShouldBe("Value");
        result.Entries["MYKEY"].ShouldBe("Value");
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
            Entries = new Dictionary<string, string> { ["App:Name"] = "Test" },
            ETag = "\"42\"",
            LastEventId = "evt-1"
        };

        // Act
        cache.Save(config);
        var loaded = cache.Load();

        // Assert
        loaded.ShouldNotBeNull();
        loaded.Entries.ShouldContainKeyAndValue("App:Name", "Test");
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