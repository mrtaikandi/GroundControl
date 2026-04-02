using GroundControl.Cli.Features.Config.Show;
using GroundControl.Cli.Shared.Config;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Config.Show;

public sealed class ShowConfigHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public ShowConfigHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"gc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "appsettings.local.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task HandleAsync_WhenNoLocalConfig_DisplaysSubtleMessage()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("No local configuration found");
    }

    [Fact]
    public async Task HandleAsync_WithAuthSection_DisplaysMaskedToken()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _settingsPath,
            """{"GroundControl": {"ServerUrl": "https://test.com", "Auth": {"Method": "pat", "Token": "gc_secret_1234"}}}""",
            TestContext.Current.CancellationToken);

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("https://test.com");
        output.ShouldContain("pat");
        output.ShouldNotContain("gc_secret_1234");
        output.ShouldContain("1234");
    }

    [Fact]
    public async Task HandleAsync_WithoutAuthSection_DisplaysNone()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _settingsPath,
            """{"GroundControl": {"ServerUrl": "https://noauth.com"}}""",
            TestContext.Current.CancellationToken);

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("https://noauth.com");
        output.ShouldContain("(none)");
    }

    private ShowConfigHandler CreateHandler(MockShellBuilder? shellBuilder = null)
    {
        shellBuilder ??= new MockShellBuilder();
        var shell = shellBuilder.Build();
        var store = new CredentialStore(_settingsPath);

        return new ShowConfigHandler(
            shell,
            Options.Create(new CliHostOptions()),
            store);
    }
}