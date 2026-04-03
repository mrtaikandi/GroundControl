using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Variables.List;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Variables.List;

public sealed class ListVariablesHandlerTests
{
    [Fact]
    public async Task HandleAsync_RendersTable()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListVariablesHandlerAsync(
                Arg.Any<VariableScope?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfVariableResponse
            {
                Data =
                [
                    CreateVariable("DbPassword", true),
                    CreateVariable("ApiUrl", false)
                ],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, new ListVariablesOptions(), OutputFormat.Table);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Id");
        output.ShouldContain("Name");
        output.ShouldContain("Sensitive");
        output.ShouldContain("DbPassword");
        output.ShouldContain("ApiUrl");
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersJsonArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var client = Substitute.For<IGroundControlClient>();
        client.ListVariablesHandlerAsync(
                Arg.Any<VariableScope?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
                Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PaginatedResponseOfVariableResponse
            {
                Data = [CreateVariable("DbPassword", true)],
                NextCursor = null
            });

        var handler = CreateHandler(shellBuilder, client, new ListVariablesOptions(), OutputFormat.Json);

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"Name\"");
        output.ShouldContain("DbPassword");
    }

    private static ListVariablesHandler CreateHandler(
        MockShellBuilder shellBuilder,
        IGroundControlClient client,
        ListVariablesOptions options,
        OutputFormat outputFormat) =>
        new(
            shellBuilder.Build(),
            Options.Create(options),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            client);

    private static VariableResponse CreateVariable(string name, bool sensitive) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Scope = VariableScope.Global,
            IsSensitive = sensitive,
            Values = [new ScopedValue { Scopes = null, Value = "test-value" }],
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}