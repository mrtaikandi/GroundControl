using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace GroundControl.Cli.Features.PersonalAccessTokens.Create;

internal sealed class CreateTokenHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CreateTokenOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public CreateTokenHandler(
        IShell shell,
        IOptions<CreateTokenOptions> options,
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

        if (_hostOptions.NoInteractive && name is null)
        {
            _shell.DisplayError("Missing required option: --name. Provide it explicitly when using --no-interactive.");
            return 1;
        }

        if (name is null)
        {
            name = await _shell.PromptForStringAsync("Token name:", cancellationToken: cancellationToken);
        }

        int? expiresInDays = null;

        if (_options.ExpiresIn is not null)
        {
            if (!TryParseExpiresIn(_options.ExpiresIn, out var days))
            {
                _shell.DisplayError($"Invalid --expires-in value '{_options.ExpiresIn}'. Use a number followed by d (days), m (months), or y (years), e.g. 30d, 6m, 1y.");
                return 1;
            }

            expiresInDays = days;
        }

        try
        {
            var request = new CreatePatRequest
            {
                Name = name,
                ExpiresInDays = expiresInDays
            };

            var token = await _client.CreatePatHandlerAsync(request, cancellationToken);

            _shell.DisplaySuccess($"Personal access token '{token.Name}' created (id: {token.Id}).");
            _shell.DisplayMessage("warning", "[yellow bold]Token value shown only once — store it securely:[/]");
            _shell.Console.MarkupLine($"[bold]{Markup.Escape(token.Token)}[/]");

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

    internal static bool TryParseExpiresIn(string value, out int days)
    {
        days = 0;

        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            return false;
        }

        var suffix = value[^1];
        var numberPart = value[..^1];

        if (!int.TryParse(numberPart, out var number) || number <= 0)
        {
            return false;
        }

        days = suffix switch
        {
            'd' => number,
            'm' => number * 30,
            'y' => number * 365,
            _ => -1
        };

        return days > 0;
    }
}