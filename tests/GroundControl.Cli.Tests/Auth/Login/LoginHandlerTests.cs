using System.Text.Json.Nodes;
using GroundControl.Cli.Features.Auth;
using GroundControl.Cli.Features.Auth.Login;
using GroundControl.Cli.Shared.Config;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Auth.Login;

public sealed class LoginHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public LoginHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"gc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "appsettings.local.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task HandleAsync_NonInteractive_PatMethod_WritesConfig()
    {
        // Arrange
        var options = new LoginOptions
        {
            ServerUrl = "https://gc.example.com",
            Method = AuthMethod.Pat,
            Token = "gc_pat_xxxx"
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Logged in");

        var section = await ReadConfigAsync();
        section.ShouldNotBeNull();
        section["ServerUrl"]!.GetValue<string>().ShouldBe("https://gc.example.com");
        section["Auth"]!["Method"]!.GetValue<string>().ShouldBe("Bearer");
        section["Auth"]!["Token"]!.GetValue<string>().ShouldBe("gc_pat_xxxx");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_ApiKeyMethod_WritesConfig()
    {
        // Arrange
        var options = new LoginOptions
        {
            ServerUrl = "https://gc.example.com",
            Method = AuthMethod.ApiKey,
            ClientId = "my-client",
            ClientSecret = "my-secret"
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);

        var section = await ReadConfigAsync();
        section.ShouldNotBeNull();
        section["Auth"]!["Method"]!.GetValue<string>().ShouldBe("ApiKey");
        section["Auth"]!["ClientId"]!.GetValue<string>().ShouldBe("my-client");
        section["Auth"]!["ClientSecret"]!.GetValue<string>().ShouldBe("my-secret");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_CredentialsMethod_WritesConfig()
    {
        // Arrange
        var options = new LoginOptions
        {
            ServerUrl = "https://gc.example.com",
            Method = AuthMethod.Credentials,
            Username = "admin",
            Password = "p@ssw0rd"
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);

        var section = await ReadConfigAsync();
        section.ShouldNotBeNull();
        section["Auth"]!["Method"]!.GetValue<string>().ShouldBe("Credentials");
        section["Auth"]!["Username"]!.GetValue<string>().ShouldBe("admin");
        section["Auth"]!["Password"]!.GetValue<string>().ShouldBe("p@ssw0rd");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_NoneMethod_WritesConfigWithoutAuth()
    {
        // Arrange
        var options = new LoginOptions
        {
            ServerUrl = "https://gc.example.com",
            Method = AuthMethod.None
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);

        var section = await ReadConfigAsync();
        section.ShouldNotBeNull();
        section["ServerUrl"]!.GetValue<string>().ShouldBe("https://gc.example.com");
        section["Auth"].ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingServerUrl_ReturnsError()
    {
        // Arrange
        var options = new LoginOptions
        {
            Method = AuthMethod.Pat,
            Token = "gc_pat_xxxx"
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--server-url");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingMethod_ReturnsError()
    {
        // Arrange
        var options = new LoginOptions
        {
            ServerUrl = "https://gc.example.com"
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--method");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_PatMissingToken_ReturnsError()
    {
        // Arrange
        var options = new LoginOptions
        {
            ServerUrl = "https://gc.example.com",
            Method = AuthMethod.Pat
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--token");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_ApiKeyMissingClientId_ReturnsError()
    {
        // Arrange
        var options = new LoginOptions
        {
            ServerUrl = "https://gc.example.com",
            Method = AuthMethod.ApiKey,
            ClientSecret = "secret"
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--client-id");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_CredentialsMissingPassword_ReturnsError()
    {
        // Arrange
        var options = new LoginOptions
        {
            ServerUrl = "https://gc.example.com",
            Method = AuthMethod.Credentials,
            Username = "admin"
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--password");
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_ListsAllMissingOptions()
    {
        // Arrange
        var options = new LoginOptions
        {
            Method = AuthMethod.ApiKey
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("--server-url");
        output.ShouldContain("--client-id");
        output.ShouldContain("--client-secret");
    }

    [Fact]
    public async Task HandleAsync_PreservesExistingSettingSections()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _settingsPath,
            """{"Logging": {"LogLevel": "Debug"}, "GroundControl": {"ServerUrl": "https://old.com"}}""",
            TestContext.Current.CancellationToken);

        var options = new LoginOptions
        {
            ServerUrl = "https://new.com",
            Method = AuthMethod.None
        };

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder, options, noInteractive: true);

        // Act
        await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        var json = await File.ReadAllTextAsync(_settingsPath, TestContext.Current.CancellationToken);
        var root = JsonNode.Parse(json)!.AsObject();
        root["Logging"].ShouldNotBeNull();
        root["GroundControl"]!["ServerUrl"]!.GetValue<string>().ShouldBe("https://new.com");
    }

    private LoginHandler CreateHandler(MockShellBuilder shellBuilder, LoginOptions options, bool noInteractive = false) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            new CredentialStore(_settingsPath));

    private async Task<JsonObject?> ReadConfigAsync()
    {
        var store = new CredentialStore(_settingsPath);
        return await store.ReadAsync(TestContext.Current.CancellationToken);
    }
}