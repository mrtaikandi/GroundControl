using System.Net.Http.Headers;
using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ApiClient;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.ClientConfig.Get;

internal sealed class GetClientConfigHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetClientConfigOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GroundControlClientOptions _clientOptions;

    public GetClientConfigHandler(
        IShell shell,
        IOptions<GetClientConfigOptions> options,
        IOptions<CliHostOptions> hostOptions,
        IHttpClientFactory httpClientFactory,
        IOptions<GroundControlClientOptions> clientOptions)
    {
        _shell = shell;
        _options = options.Value;
        _hostOptions = hostOptions.Value;
        _httpClientFactory = httpClientFactory;
        _clientOptions = clientOptions.Value;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(_clientOptions.ServerUrl);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("ApiKey", $"{_options.ClientId}:{_options.ClientSecret}");

        var client = new GroundControlClient(httpClient);

        var (exitCode, response) = await _shell.TryCallAsync(
            ct => client.GetConfigHandlerAsync(ct), cancellationToken);

        if (exitCode != 0)
        {
            return exitCode;
        }

        if (_hostOptions.OutputFormat == OutputFormat.Json)
        {
            _shell.RenderJson(response!);
            return 0;
        }

        RenderConfigTable(response!);
        return 0;
    }

    private void RenderConfigTable(ClientConfigResponse response)
    {
        _shell.RenderDetail(
        [
            ("Snapshot Id", response.SnapshotId.ToString()),
            ("Snapshot Version", response.SnapshotVersion.ToString(CultureInfo.InvariantCulture))
        ],
        _hostOptions.OutputFormat);

        if (response.Data.Count == 0)
        {
            return;
        }

        _shell.DisplayEmptyLine();

        var items = response.Data
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _shell.RenderTable(
            items: items,
            headers: ["Key", "Value", "Sensitive"],
            valueExtractors: [kv => kv.Key, kv => kv.Value.Value, kv => (kv.Value.IsSensitive == true).ToString()],
            outputFormat: _hostOptions.OutputFormat);
    }
}