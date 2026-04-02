using System.Text.Json.Nodes;
using GroundControl.Cli.Features.Config.Import;
using GroundControl.Cli.Shared.Config;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Config.Import;

public sealed class ImportConfigHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public ImportConfigHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"gc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "appsettings.local.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task HandleAsync_ImportFromFile_WritesConfig()
    {
        // Arrange
        var configFile = Path.Combine(_tempDir, "import.json");
        await File.WriteAllTextAsync(
            configFile,
            """{"ServerUrl": "https://imported.example.com"}""",
            TestContext.Current.CancellationToken);

        var handler = CreateHandler(
            new ImportConfigOptions { FilePath = configFile, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        File.Exists(_settingsPath).ShouldBeTrue();
        var root = JsonNode.Parse(await File.ReadAllTextAsync(_settingsPath, TestContext.Current.CancellationToken))!.AsObject();
        root["GroundControl"]!["ServerUrl"]!.GetValue<string>().ShouldBe("https://imported.example.com");
    }

    [Fact]
    public async Task HandleAsync_ImportFromFileWithWrapper_ExtractsSection()
    {
        // Arrange
        var configFile = Path.Combine(_tempDir, "import.json");
        await File.WriteAllTextAsync(
            configFile,
            """{"GroundControl": {"ServerUrl": "https://wrapped.example.com", "Auth": {"Method": "pat", "Token": "secret123"}}}""",
            TestContext.Current.CancellationToken);

        var handler = CreateHandler(
            new ImportConfigOptions { FilePath = configFile, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(_settingsPath, TestContext.Current.CancellationToken))!.AsObject();
        root["GroundControl"]!["ServerUrl"]!.GetValue<string>().ShouldBe("https://wrapped.example.com");
        root["GroundControl"]!["Auth"]!["Method"]!.GetValue<string>().ShouldBe("pat");
    }

    [Fact]
    public async Task HandleAsync_ImportWithOnlyServerUrl_WorksForNoAuth()
    {
        // Arrange
        var configFile = Path.Combine(_tempDir, "import.json");
        await File.WriteAllTextAsync(
            configFile,
            """{"ServerUrl": "https://noauth.example.com"}""",
            TestContext.Current.CancellationToken);

        var handler = CreateHandler(
            new ImportConfigOptions { FilePath = configFile, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(_settingsPath, TestContext.Current.CancellationToken))!.AsObject();
        root["GroundControl"]!["ServerUrl"]!.GetValue<string>().ShouldBe("https://noauth.example.com");
    }

    [Fact]
    public async Task HandleAsync_ImportInvalidJson_ReturnsError()
    {
        // Arrange
        var configFile = Path.Combine(_tempDir, "bad.json");
        await File.WriteAllTextAsync(configFile, "not json {{{", TestContext.Current.CancellationToken);

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(
            new ImportConfigOptions { FilePath = configFile, Yes = true },
            shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Invalid JSON");
    }

    [Fact]
    public async Task HandleAsync_ImportMissingServerUrl_ReturnsError()
    {
        // Arrange
        var configFile = Path.Combine(_tempDir, "incomplete.json");
        await File.WriteAllTextAsync(
            configFile,
            """{"Auth": {"Method": "pat"}}""",
            TestContext.Current.CancellationToken);

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(
            new ImportConfigOptions { FilePath = configFile, Yes = true },
            shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Missing required property 'ServerUrl'");
    }

    [Fact]
    public async Task HandleAsync_FileNotFound_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(
            new ImportConfigOptions { FilePath = "/nonexistent/file.json", Yes = true },
            shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("File not found");
    }

    [Fact]
    public async Task HandleAsync_PasteMode_ParsesAndWritesConfig()
    {
        // Arrange
        var pastedJson = """{"ServerUrl": "https://pasted.example.com"}""" + "\n\n";
        var shellBuilder = new MockShellBuilder().WithInput(pastedJson);
        var handler = CreateHandler(
            new ImportConfigOptions { Paste = true, Yes = true },
            shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        File.Exists(_settingsPath).ShouldBeTrue();
        var root = JsonNode.Parse(await File.ReadAllTextAsync(_settingsPath, TestContext.Current.CancellationToken))!.AsObject();
        root["GroundControl"]!["ServerUrl"]!.GetValue<string>().ShouldBe("https://pasted.example.com");
    }

    [Fact]
    public async Task HandleAsync_PasteMode_EmptyInput_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder().WithInput("\n");
        var handler = CreateHandler(
            new ImportConfigOptions { Paste = true, Yes = true },
            shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("No input received");
    }

    [Fact]
    public async Task HandleAsync_MergePreservesExistingSections()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _settingsPath,
            """{"Logging": {"Level": "Warning"}, "GroundControl": {"ServerUrl": "https://old.com"}}""",
            TestContext.Current.CancellationToken);

        var configFile = Path.Combine(_tempDir, "import.json");
        await File.WriteAllTextAsync(
            configFile,
            """{"ServerUrl": "https://new.com"}""",
            TestContext.Current.CancellationToken);

        var handler = CreateHandler(
            new ImportConfigOptions { FilePath = configFile, Yes = true });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(_settingsPath, TestContext.Current.CancellationToken))!.AsObject();
        root["Logging"]!["Level"]!.GetValue<string>().ShouldBe("Warning");
        root["GroundControl"]!["ServerUrl"]!.GetValue<string>().ShouldBe("https://new.com");
    }

    private ImportConfigHandler CreateHandler(
        ImportConfigOptions options,
        MockShellBuilder? shellBuilder = null)
    {
        shellBuilder ??= new MockShellBuilder();
        var shell = shellBuilder.Build();
        var store = new CredentialStore(_settingsPath);

        return new ImportConfigHandler(
            shell,
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = true }),
            store);
    }
}