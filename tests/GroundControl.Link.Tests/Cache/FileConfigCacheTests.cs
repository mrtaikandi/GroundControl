using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Link.Tests.Cache;

public sealed class FileConfigCacheTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly string _cachePath;

    public FileConfigCacheTests()
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
        var cache = CreateCache();

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
        var cache = CreateCache();

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
        var cache = CreateCache();

        // Act
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ReturnsSameConfig()
    {
        // Arrange
        var cache = CreateCache();
        var config = new Dictionary<string, string>
        {
            ["Logging:LogLevel:Default"] = "Warning",
            ["Database:Host"] = "localhost",
            ["FeatureFlags:DarkMode"] = "true"
        };

        // Act
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result["Logging:LogLevel:Default"].ShouldBe("Warning");
        result["Database:Host"].ShouldBe("localhost");
        result["FeatureFlags:DarkMode"].ShouldBe("true");
    }

    [Fact]
    public async Task SaveAsync_CreatesFile()
    {
        // Arrange
        var cache = CreateCache();
        var config = new Dictionary<string, string> { ["Key1"] = "Value1" };

        // Act
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(_cachePath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var nestedDir = Path.Combine(_cacheDir, "nested", "deep");
        var nestedPath = Path.Combine(nestedDir, "cache.json");
        var cache = CreateCache(cachePath: nestedPath);
        var config = new Dictionary<string, string> { ["Key1"] = "Value1" };

        // Act
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(nestedPath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFile()
    {
        // Arrange
        var cache = CreateCache();
        var config1 = new Dictionary<string, string> { ["Key1"] = "Original" };
        var config2 = new Dictionary<string, string> { ["Key1"] = "Updated", ["Key2"] = "New" };

        // Act
        await cache.SaveAsync(config1, TestContext.Current.CancellationToken);
        await cache.SaveAsync(config2, TestContext.Current.CancellationToken);
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result["Key1"].ShouldBe("Updated");
        result["Key2"].ShouldBe("New");
    }

    [Fact]
    public async Task SaveAsync_WithDataProtection_EncryptsValuesInFile()
    {
        // Arrange
        var (provider, _) = CreateMockDataProtection();
        var cache = CreateCache(dataProtection: provider);
        var config = new Dictionary<string, string> { ["Secret"] = "my-password" };

        // Act
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);

        // Assert
        var rawJson = await File.ReadAllTextAsync(_cachePath, TestContext.Current.CancellationToken);
        rawJson.ShouldContain("***ENCRYPTED:");
        rawJson.ShouldNotContain("my-password");
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_WithDataProtection_DecryptsValues()
    {
        // Arrange
        var (provider, _) = CreateMockDataProtection();
        var cache = CreateCache(dataProtection: provider);
        var config = new Dictionary<string, string>
        {
            ["Secret"] = "my-password",
            ["ApiKey"] = "abc-123"
        };

        // Act
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result["Secret"].ShouldBe("my-password");
        result["ApiKey"].ShouldBe("abc-123");
    }

    [Fact]
    public async Task SaveAsync_WithoutDataProtection_StoresPlaintext()
    {
        // Arrange
        var cache = CreateCache();
        var config = new Dictionary<string, string> { ["Key1"] = "plaintext-value" };

        // Act
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);

        // Assert
        var rawJson = await File.ReadAllTextAsync(_cachePath, TestContext.Current.CancellationToken);
        rawJson.ShouldContain("plaintext-value");
        rawJson.ShouldNotContain("***ENCRYPTED:");
    }

    [Fact]
    public async Task LoadAsync_EncryptedValuesWithoutProtector_ReturnsNull()
    {
        // Arrange — write encrypted values using DataProtection
        var (provider, _) = CreateMockDataProtection();
        var encryptedCache = CreateCache(dataProtection: provider);
        var config = new Dictionary<string, string> { ["Secret"] = "my-password" };
        await encryptedCache.SaveAsync(config, TestContext.Current.CancellationToken);

        // Act — read without DataProtection
        var plainCache = CreateCache();
        var result = await plainCache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CacheFilePath_FromOptions_IsRespected()
    {
        // Arrange
        var customPath = Path.Combine(_cacheDir, "custom", "path.json");
        var cache = CreateCache(cachePath: customPath);
        var config = new Dictionary<string, string> { ["Key1"] = "Value1" };

        // Act
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(customPath).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_ConcurrentWrites_NoCorruption()
    {
        // Arrange
        var cache = CreateCache();
        var tasks = new List<Task>();

        // Act — fire multiple concurrent writes
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(cache.SaveAsync(
                new Dictionary<string, string> { ["Key"] = $"Value{index}" },
                TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks);

        // Assert — the file should be readable and contain a valid config
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.ShouldContainKey("Key");
        result["Key"].ShouldStartWith("Value");
    }

    [Fact]
    public async Task SaveAsync_CacheFileContainsTimestamp()
    {
        // Arrange
        var cache = CreateCache();
        var config = new Dictionary<string, string> { ["Key1"] = "Value1" };

        // Act
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);

        // Assert
        var rawJson = await File.ReadAllTextAsync(_cachePath, TestContext.Current.CancellationToken);
        rawJson.ShouldContain("timestamp");
    }

    [Fact]
    public async Task LoadAsync_CaseInsensitiveKeys()
    {
        // Arrange
        var cache = CreateCache();
        var config = new Dictionary<string, string> { ["MyKey"] = "Value" };
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);

        // Act
        var result = await cache.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result["mykey"].ShouldBe("Value");
        result["MYKEY"].ShouldBe("Value");
    }

    private FileConfigCache CreateCache(string? cachePath = null, IDataProtectionProvider? dataProtection = null) =>
        new(
            new GroundControlOptions
            {
                ServerUrl = "http://localhost",
                ClientId = "test-client",
                ClientSecret = "test-secret",
                CacheFilePath = cachePath ?? _cachePath
            },
            NullLogger<FileConfigCache>.Instance,
            dataProtection);

    // Mocks the byte[] overloads — the string Protect/Unprotect extension methods
    // in DataProtectionCommonExtensions delegate to these via UTF-8 + Base64Url encoding.
    private static (IDataProtectionProvider Provider, IDataProtector Protector) CreateMockDataProtection()
    {
        var protector = Substitute.For<IDataProtector>();

        protector.Protect(Arg.Any<byte[]>())
            .Returns(callInfo =>
            {
                var input = callInfo.Arg<byte[]>();
                var output = new byte[input.Length];
                for (var i = 0; i < input.Length; i++)
                {
                    output[i] = (byte)(input[i] ^ 0xFF);
                }

                return output;
            });

        protector.Unprotect(Arg.Any<byte[]>())
            .Returns(callInfo =>
            {
                var input = callInfo.Arg<byte[]>();
                var output = new byte[input.Length];
                for (var i = 0; i < input.Length; i++)
                {
                    output[i] = (byte)(input[i] ^ 0xFF);
                }

                return output;
            });

        var provider = Substitute.For<IDataProtectionProvider>();
        provider.CreateProtector(Arg.Any<string>()).Returns(protector);

        return (provider, protector);
    }
}