using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Variables.Get;

internal sealed class GetVariableHandler : ICommandHandler
{
    private const string MaskedValue = "********";

    private readonly IShell _shell;
    private readonly GetVariableOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetVariableHandler(
        IShell shell,
        IOptions<GetVariableOptions> options,
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
        try
        {
            var variable = await _client.GetVariableHandlerAsync(_options.Id, _options.Decrypt, cancellationToken);
            _shell.RenderDetail(BuildDetail(variable), _hostOptions.OutputFormat);
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private List<(string Key, string Value)> BuildDetail(VariableResponse variable)
    {
        var details = new List<(string Key, string Value)>
        {
            ("Id", variable.Id.ToString()),
            ("Name", variable.Name),
            ("Scope", variable.Scope.ToString()),
            ("Group Id", variable.GroupId?.ToString() ?? string.Empty),
            ("Project Id", variable.ProjectId?.ToString() ?? string.Empty),
            ("Sensitive", variable.IsSensitive.ToString())
        };

        var shouldMask = variable.IsSensitive && _options.Decrypt != true;

        foreach (var sv in variable.Values)
        {
            var scopeLabel = sv.Scopes is { Count: > 0 }
                ? string.Join(", ", sv.Scopes.Select(s => $"{s.Key}:{s.Value}"))
                : "default";
            var displayValue = shouldMask ? MaskedValue : sv.Value;
            details.Add(($"Value [{scopeLabel}]", displayValue));
        }

        details.Add(("Description", variable.Description ?? string.Empty));
        details.Add(("Version", variable.Version.ToString(CultureInfo.InvariantCulture)));
        details.Add(("Created At", variable.CreatedAt.ToString("O")));
        details.Add(("Updated At", variable.UpdatedAt.ToString("O")));

        return details;
    }
}