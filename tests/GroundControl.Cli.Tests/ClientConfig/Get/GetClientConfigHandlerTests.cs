using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.ClientConfig.Get;
using GroundControl.Cli.Shared.ApiClient;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.ClientConfig.Get;

public sealed class GetClientConfigHandlerTests
{
    private static readonly Guid ClientId = Guid.CreateVersion7();
    private const string ClientSecret = "test-secret";

    [Fact]
    public async Task HandleAsync_RendersConfigTable()
    {
        // Arrange
        var snapshotId = Guid.CreateVersion7();
        var shellBuilder = new MockShellBuilder();
        var (factory, handler) = CreateHandler(
            shellBuilder,
            OutputFormat.Table,
            new ClientConfigResponse
            {
                SnapshotId = snapshotId,
                SnapshotVersion = 42,
                Data = new Dictionary<string, string>
                {
                    ["database:host"] = "localhost",
                    ["app:name"] = "MyApp"
                }
            });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Snapshot Id");
        output.ShouldContain(snapshotId.ToString());
        output.ShouldContain("42");
        output.ShouldContain("database:host");
        output.ShouldContain("localhost");
        output.ShouldContain("app:name");
        output.ShouldContain("MyApp");
        factory.Received(1).CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_EmptyData_RendersSnapshotOnly()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var (_, handler) = CreateHandler(
            shellBuilder,
            OutputFormat.Table,
            new ClientConfigResponse
            {
                SnapshotId = Guid.CreateVersion7(),
                SnapshotVersion = 1,
                Data = new Dictionary<string, string>()
            });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Snapshot Id");
        output.ShouldContain("Snapshot Version");
        output.ShouldNotContain("Key");
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_RendersRawResponse()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var (_, handler) = CreateHandler(
            shellBuilder,
            OutputFormat.Json,
            new ClientConfigResponse
            {
                SnapshotId = Guid.CreateVersion7(),
                SnapshotVersion = 5,
                Data = new Dictionary<string, string> { ["key1"] = "value1" }
            });

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("\"snapshotVersion\"");
        output.ShouldContain("\"key1\"");
        output.ShouldContain("\"value1\"");
    }

    [Fact]
    public async Task HandleAsync_ApiError_ShowsErrorAndReturnsOne()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var (_, handler) = CreateHandler(shellBuilder, OutputFormat.Table, problemDetail: "No active snapshot found.");

        // Act
        var exitCode = await handler.HandleAsync(TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        shellBuilder.GetOutput().ShouldContain("No active snapshot found.");
    }

    private static (IHttpClientFactory Factory, GetClientConfigHandler Handler) CreateHandler(
        MockShellBuilder shellBuilder,
        OutputFormat outputFormat,
        ClientConfigResponse? response = null,
        string? problemDetail = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();

#pragma warning disable CA2000 // Ownership transfers to the handler under test which disposes via 'using'
        var httpClient = new HttpClient(new FakeHttpHandler(response, problemDetail))
        {
            BaseAddress = new Uri("https://localhost:5001")
        };
#pragma warning restore CA2000

        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var handler = new GetClientConfigHandler(
            shellBuilder.Build(),
            Options.Create(new GetClientConfigOptions { ClientId = ClientId, ClientSecret = ClientSecret }),
            Options.Create(new CliHostOptions { OutputFormat = outputFormat }),
            factory,
            Options.Create(new GroundControlClientOptions { ServerUrl = "https://localhost:5001" }));

        return (factory, handler);
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly ClientConfigResponse? _response;
        private readonly string? _problemDetail;

        public FakeHttpHandler(ClientConfigResponse? response, string? problemDetail)
        {
            _response = response;
            _problemDetail = problemDetail;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_problemDetail is not null)
            {
                var errorContent = System.Text.Json.JsonSerializer.Serialize(new ProblemDetails
                {
                    Status = 404,
                    Detail = _problemDetail
                });

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                {
                    Content = new StringContent(errorContent, System.Text.Encoding.UTF8, "application/problem+json")
                });
            }

            var content = System.Text.Json.JsonSerializer.Serialize(_response);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}