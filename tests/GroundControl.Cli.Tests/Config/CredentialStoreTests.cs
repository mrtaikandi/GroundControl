using System.Text.Json.Nodes;
using GroundControl.Cli.Shared.Config;

namespace GroundControl.Cli.Tests.Config;

public sealed class CredentialStoreTests : IDisposable
{
    private readonly string _tempDir;

    public CredentialStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"gc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task ReadAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var store = new CredentialStore(Path.Combine(_tempDir, "missing.json"));

        // Act
        var result = await store.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task WriteAsync_CreatesFileWithGroundControlSection()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "settings.json");
        var store = new CredentialStore(path);
        var section = new JsonObject { ["ServerUrl"] = "https://example.com" };

        // Act
        await store.WriteAsync(section, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(path).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        var root = JsonNode.Parse(content)!.AsObject();
        root["GroundControl"]!["ServerUrl"]!.GetValue<string>().ShouldBe("https://example.com");
    }

    [Fact]
    public async Task WriteAsync_PreservesExistingSections()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(
            path,
            """{"Logging": {"Level": "Debug"}, "GroundControl": {"ServerUrl": "https://old.com"}}""",
            TestContext.Current.CancellationToken);

        var store = new CredentialStore(path);
        var section = new JsonObject { ["ServerUrl"] = "https://new.com" };

        // Act
        await store.WriteAsync(section, TestContext.Current.CancellationToken);

        // Assert
        var content = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        var root = JsonNode.Parse(content)!.AsObject();
        root["Logging"]!["Level"]!.GetValue<string>().ShouldBe("Debug");
        root["GroundControl"]!["ServerUrl"]!.GetValue<string>().ShouldBe("https://new.com");
    }

    [Fact]
    public async Task ReadAsync_ReturnsGroundControlSection()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(
            path,
            """{"GroundControl": {"ServerUrl": "https://test.com", "Auth": {"Method": "pat"}}}""",
            TestContext.Current.CancellationToken);

        var store = new CredentialStore(path);

        // Act
        var result = await store.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result["ServerUrl"]!.GetValue<string>().ShouldBe("https://test.com");
        result["Auth"]!["Method"]!.GetValue<string>().ShouldBe("pat");
    }

    [Fact]
    public void TryParseConfig_WithValidJson_ReturnsSection()
    {
        // Arrange
        var json = """{"ServerUrl": "https://example.com"}""";

        // Act
        var success = CredentialStore.TryParseConfig(json, out var section, out var error);

        // Assert
        success.ShouldBeTrue();
        error.ShouldBeNull();
        section.ShouldNotBeNull();
        section["ServerUrl"]!.GetValue<string>().ShouldBe("https://example.com");
    }

    [Fact]
    public void TryParseConfig_WithWrapperKey_ExtractsInnerObject()
    {
        // Arrange
        var json = """{"GroundControl": {"ServerUrl": "https://wrapped.com"}}""";

        // Act
        var success = CredentialStore.TryParseConfig(json, out var section, out _);

        // Assert
        success.ShouldBeTrue();
        section!["ServerUrl"]!.GetValue<string>().ShouldBe("https://wrapped.com");
    }

    [Fact]
    public void TryParseConfig_WithOnlyServerUrl_Succeeds()
    {
        // Arrange
        var json = """{"ServerUrl": "https://noauth.com"}""";

        // Act
        var success = CredentialStore.TryParseConfig(json, out var section, out _);

        // Assert
        success.ShouldBeTrue();
        section!["ServerUrl"]!.GetValue<string>().ShouldBe("https://noauth.com");
    }

    [Fact]
    public void TryParseConfig_WithInvalidJson_ReturnsFalse()
    {
        // Arrange
        var json = "not json {{{";

        // Act
        var success = CredentialStore.TryParseConfig(json, out _, out var error);

        // Assert
        success.ShouldBeFalse();
        error.ShouldStartWith("Invalid JSON:");
    }

    [Fact]
    public void TryParseConfig_WithMissingServerUrl_ReturnsFalse()
    {
        // Arrange
        var json = """{"Auth": {"Method": "pat"}}""";

        // Act
        var success = CredentialStore.TryParseConfig(json, out _, out var error);

        // Assert
        success.ShouldBeFalse();
        error.ShouldBe("Missing required property 'ServerUrl'.");
    }

    [Fact]
    public void TryParseConfig_WithEmptyServerUrl_ReturnsFalse()
    {
        // Arrange
        var json = """{"ServerUrl": "  "}""";

        // Act
        var success = CredentialStore.TryParseConfig(json, out _, out var error);

        // Assert
        success.ShouldBeFalse();
        error.ShouldBe("Missing required property 'ServerUrl'.");
    }

    [Theory]
    [InlineData(null, "(not set)")]
    [InlineData("", "(not set)")]
    [InlineData("abc", "***")]
    [InlineData("abcd", "****")]
    [InlineData("gc_pat_xxxx1234", "***********1234")]
    public void MaskValue_MasksSensitiveData(string? input, string expected)
    {
        // Act
        var result = CredentialStore.MaskValue(input);

        // Assert
        result.ShouldBe(expected);
    }
}