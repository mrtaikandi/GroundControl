using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Templates.Get;

internal sealed class GetTemplateHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetTemplateOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetTemplateHandler(
        IShell shell,
        IOptions<GetTemplateOptions> options,
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
            var template = await _client.GetTemplateHandlerAsync(_options.Id, cancellationToken);
            _shell.RenderDetail(BuildDetail(template), _hostOptions.OutputFormat);
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private static IReadOnlyList<(string Key, string Value)> BuildDetail(TemplateResponse template) =>
    [
        ("Id", template.Id.ToString()),
        ("Name", template.Name),
        ("Description", template.Description ?? string.Empty),
        ("GroupId", template.GroupId?.ToString() ?? string.Empty),
        ("Version", template.Version.ToString()),
        ("Created At", template.CreatedAt.ToString("O")),
        ("Updated At", template.UpdatedAt.ToString("O"))
    ];
}