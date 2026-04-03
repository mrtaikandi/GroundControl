using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.PersonalAccessTokens.Create;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.PersonalAccessTokens.Create;

public sealed class CreateTokenHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithName_CreatesToken()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreatePatHandlerAsync(Arg.Any<CreatePatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePatResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "CI",
                Token = "gc_abc123secret",
                TokenPrefix = "gc_ab",
                ExpiresAt = null,
                CreatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateTokenOptions { Name = "CI" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("CI");
        output.ShouldContain("created");
        output.ShouldContain("gc_abc123secret");
        output.ShouldContain("only once");
        await client.Received(1).CreatePatHandlerAsync(
            Arg.Is<CreatePatRequest>(r => r.Name == "CI" && r.ExpiresInDays == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithExpiresIn_ParsesDaysAndSendsToApi()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreatePatHandlerAsync(Arg.Any<CreatePatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePatResponse
            {
                Id = Guid.CreateVersion7(),
                Name = "Dev",
                Token = "gc_devtoken",
                TokenPrefix = "gc_dv",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                CreatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client,
            new CreateTokenOptions { Name = "Dev", ExpiresIn = "30d" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        await client.Received(1).CreatePatHandlerAsync(
            Arg.Is<CreatePatRequest>(r => r.Name == "Dev" && r.ExpiresInDays == 30),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonInteractive_MissingName_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateTokenOptions(),
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("--name");
        await client.DidNotReceive().CreatePatHandlerAsync(
            Arg.Any<CreatePatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidExpiresIn_ReturnsError()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();

        var handler = CreateHandler(shellBuilder, client,
            new CreateTokenOptions { Name = "CI", ExpiresIn = "invalid" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Invalid --expires-in");
        await client.DidNotReceive().CreatePatHandlerAsync(
            Arg.Any<CreatePatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ApiValidationError_ShowsProblemDetails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.CreatePatHandlerAsync(Arg.Any<CreatePatRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<HttpValidationProblemDetails>(
                "Bad Request", 400, null, new Dictionary<string, IEnumerable<string>>(),
                new HttpValidationProblemDetails
                {
                    Status = 400,
                    Detail = "Validation failed.",
                    Errors = new Dictionary<string, ICollection<string>>
                    {
                        ["Name"] = ["Name is required."]
                    }
                }, null));

        var handler = CreateHandler(shellBuilder, client,
            new CreateTokenOptions { Name = "" },
            noInteractive: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Validation failed");
    }

    private static CreateTokenHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        CreateTokenOptions options,
        bool noInteractive) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { NoInteractive = noInteractive }),
            client);
}

public sealed class TryParseExpiresInTests
{
    [Theory]
    [InlineData("30d", 30)]
    [InlineData("1d", 1)]
    [InlineData("6m", 180)]
    [InlineData("1m", 30)]
    [InlineData("1y", 365)]
    [InlineData("2y", 730)]
    public void TryParseExpiresIn_ValidInput_ReturnsDays(string input, int expectedDays)
    {
        // Act
        var result = CreateTokenHandler.TryParseExpiresIn(input, out var days);

        // Assert
        result.ShouldBeTrue();
        days.ShouldBe(expectedDays);
    }

    [Theory]
    [InlineData("")]
    [InlineData("d")]
    [InlineData("invalid")]
    [InlineData("30x")]
    [InlineData("-1d")]
    [InlineData("0d")]
    public void TryParseExpiresIn_InvalidInput_ReturnsFalse(string input)
    {
        // Act
        var result = CreateTokenHandler.TryParseExpiresIn(input, out _);

        // Assert
        result.ShouldBeFalse();
    }
}