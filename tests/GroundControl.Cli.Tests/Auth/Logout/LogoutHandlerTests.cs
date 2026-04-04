using GroundControl.Cli.Features.Auth.Logout;
using GroundControl.Cli.Shared.Config;

namespace GroundControl.Cli.Tests.Auth.Logout;

public sealed class LogoutHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public LogoutHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"gc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "appsettings.local.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task HandleAsync_WhenNoConfig_DisplaysAlreadyLoggedOut()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Already logged out");
    }

    [Fact]
    public async Task HandleAsync_WithAuthSection_ClearsAuth()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _settingsPath,
            """{"GroundControl": {"ServerUrl": "https://test.com", "Auth": {"Method": "Bearer", "Token": "secret"}}}""",
            TestContext.Current.CancellationToken);

        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Logged out");

        var store = new CredentialStore(_settingsPath);
        var section = await store.ReadAsync(TestContext.Current.CancellationToken);
        section.ShouldNotBeNull();
        section["ServerUrl"]!.GetValue<string>().ShouldBe("https://test.com");
        section["Auth"].ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithoutAuthSection_DisplaysAlreadyLoggedOutAndDoesNotWrite()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _settingsPath,
            """{"GroundControl": {"ServerUrl": "https://test.com"}}""",
            TestContext.Current.CancellationToken);

        var lastWriteTime = File.GetLastWriteTimeUtc(_settingsPath);
        var shellBuilder = new MockShellBuilder();
        var handler = CreateHandler(shellBuilder);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        shellBuilder.GetOutput().ShouldContain("Already logged out");
        File.GetLastWriteTimeUtc(_settingsPath).ShouldBe(lastWriteTime);
    }

    private LogoutHandler CreateHandler(MockShellBuilder shellBuilder) =>
        new(shellBuilder.Build(), new CredentialStore(_settingsPath));
}