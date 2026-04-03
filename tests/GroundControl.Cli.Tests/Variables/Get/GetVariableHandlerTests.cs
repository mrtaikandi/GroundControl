using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Variables.Get;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace GroundControl.Cli.Tests.Variables.Get;

public sealed class GetVariableHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersVariableDetail()
    {
        // Arrange
        var varId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetVariableHandlerAsync(varId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new VariableResponse
            {
                Id = varId,
                Name = "ConnectionString",
                Scope = VariableScope.Global,
                IsSensitive = false,
                Values =
                [
                    new ScopedValue { Scopes = null, Value = "localhost" },
                    new ScopedValue
                    {
                        Scopes = new Dictionary<string, string> { ["env"] = "prod" },
                        Value = "sql.prod.internal"
                    }
                ],
                Version = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, varId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("ConnectionString");
        output.ShouldContain("localhost");
        output.ShouldContain("sql.prod.internal");
        output.ShouldContain("env:prod");
        output.ShouldContain("default");
    }

    [Fact]
    public async Task HandleAsync_SensitiveWithoutDecrypt_MasksValues()
    {
        // Arrange
        var varId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetVariableHandlerAsync(varId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(new VariableResponse
            {
                Id = varId,
                Name = "Secret",
                Scope = VariableScope.Global,
                IsSensitive = true,
                Values = [new ScopedValue { Scopes = null, Value = "encrypted-blob" }],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, varId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("********");
        output.ShouldNotContain("encrypted-blob");
    }

    [Fact]
    public async Task HandleAsync_SensitiveWithDecrypt_ShowsValues()
    {
        // Arrange
        var varId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetVariableHandlerAsync(varId, true, Arg.Any<CancellationToken>())
            .Returns(new VariableResponse
            {
                Id = varId,
                Name = "Secret",
                Scope = VariableScope.Global,
                IsSensitive = true,
                Values = [new ScopedValue { Scopes = null, Value = "decrypted-value" }],
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var handler = CreateHandler(shellBuilder, client, varId, OutputFormat.Table, decrypt: true);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("decrypted-value");
        output.ShouldNotContain("********");
    }

    [Fact]
    public async Task HandleAsync_NotFound_ShowsError()
    {
        // Arrange
        var varId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.GetVariableHandlerAsync(varId, Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GroundControlApiClientException<ProblemDetails>(
                "Not Found", 404, null, new Dictionary<string, IEnumerable<string>>(),
                new ProblemDetails { Status = 404, Detail = "Variable not found." }, null));

        var handler = CreateHandler(shellBuilder, client, varId, OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("Variable not found.");
    }

    private static GetVariableHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        Guid id,
        OutputFormat outputFormat,
        bool? decrypt = null) =>
        new(
            shellBuilder.Build(),
            Options.Create(new GetVariableOptions { Id = id, Decrypt = decrypt }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);
}