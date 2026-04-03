using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Clients.Create;

internal sealed class CreateClientHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CreateClientOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public CreateClientHandler(
        IShell shell,
        IOptions<CreateClientOptions> options,
        IOptions<CliHostOptions> hostOptions,
        IGroundControlClient client)
    {
        _shell = shell;
        _options = options.Value;
        _hostOptions = hostOptions.Value;
        _client = client;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var name = _options.Name;

        if (name is null && _hostOptions.NoInteractive)
        {
            _shell.DisplayError("Missing required option: --name. Provide it explicitly when using --no-interactive.");
            return 1;
        }

        if (name is null)
        {
            name = await _shell.PromptForStringAsync("Client name:", cancellationToken: cancellationToken);
        }

        var scopes = ParseScopes(_options.Scopes);

        try
        {
            var request = new CreateClientRequest
            {
                Name = name,
                Scopes = scopes,
                ExpiresAt = _options.ExpiresAt
            };

            var result = await _client.CreateClientHandlerAsync(_options.ProjectId, request, cancellationToken);

            _shell.DisplaySuccess($"Client '{result.Name}' created (id: {result.Id}, version: {result.Version}).");
            _shell.DisplayEmptyLine();
            _shell.DisplayMessage("warning", "[yellow bold]The client secret is shown only once. Save it now — you will not be able to retrieve it later.[/]");
            _shell.DisplayEmptyLine();
            _shell.DisplayMessage($"  Client ID:     [bold]{result.Id}[/]");
            _shell.DisplayMessage($"  Client Secret: [bold]{result.ClientSecret}[/]");
            _shell.DisplayEmptyLine();

            return 0;
        }
        catch (GroundControlApiClientException<HttpValidationProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private static Dictionary<string, string>? ParseScopes(string? scopesCsv)
    {
        if (scopesCsv is null)
        {
            return null;
        }

        var parts = scopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scopes = new Dictionary<string, string>(parts.Length);

        foreach (var part in parts)
        {
            var eqIndex = part.IndexOf('=', StringComparison.Ordinal);
            if (eqIndex > 0 && eqIndex < part.Length - 1)
            {
                scopes[part[..eqIndex]] = part[(eqIndex + 1)..];
            }
        }

        return scopes.Count > 0 ? scopes : null;
    }
}