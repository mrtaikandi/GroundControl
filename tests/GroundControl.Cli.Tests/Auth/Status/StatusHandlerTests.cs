using GroundControl.Cli.Features.Auth.Status;
using GroundControl.Cli.Shared.Config;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Auth.Status;

public sealed class StatusHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public StatusHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"gc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "appsettings.local.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task HandleAsync_WhenNoConfig_DisplaysNotConfiguredMessage()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Not configured");
    }

    [Fact]
    public async Task HandleAsync_WithAuthConfig_DisplaysServerAndMethod()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _settingsPath,
            """{"GroundControl": {"ServerUrl": "https://gc.example.com", "Auth": {"Method": "Bearer", "Token": "gc_pat_secret1234"}}}""",
            TestContext.Current.CancellationToken);

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("https://gc.example.com");
        output.ShouldContain("Bearer");
        output.ShouldNotContain("gc_pat_secret1234");
        output.ShouldContain("1234");
    }

    [Fact]
    public async Task HandleAsync_WithoutAuth_DisplaysNone()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _settingsPath,
            """{"GroundControl": {"ServerUrl": "https://gc.example.com"}}""",
            TestContext.Current.CancellationToken);

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("https://gc.example.com");
        output.ShouldContain("(none)");
    }

    private StatusHandler CreateHandler(MockShellBuilder shellBuilder) =>
        new(
            shellBuilder.Build(),
            Options.Create(new CliHostOptions()),
            new CredentialStore(_settingsPath));
}