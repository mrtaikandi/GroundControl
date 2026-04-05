using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Scopes.Create;

internal sealed class CreateScopeHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CreateScopeOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public CreateScopeHandler(
        IShell shell,
        IOptions<CreateScopeOptions> options,
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
        var dimension = _options.Dimension;
        var values = _options.Values;

        if (_hostOptions.NoInteractive && (dimension is null || values is null))
        {
            var missing = new List<string>();
            if (dimension is null)
            {
                missing.Add("--dimension");
            }

            if (values is null)
            {
                missing.Add("--values");
            }

            _shell.DisplayError($"Missing required option(s): {string.Join(", ", missing)}. Provide them explicitly when using --no-interactive.");
            return 1;
        }

        dimension ??= await _shell.PromptForStringAsync("Dimension name:", cancellationToken: cancellationToken);

        values ??= await _shell.PromptForStringAsync("Allowed values (comma-separated):", cancellationToken: cancellationToken);

        var description = _options.Description;
        if (description is null && !_hostOptions.NoInteractive)
        {
            description = await _shell.PromptForStringAsync("Description:", isOptional: true, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(description))
            {
                description = null;
            }
        }

        var allowedValues = values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var request = new CreateScopeRequest
        {
            Dimension = dimension,
            AllowedValues = allowedValues,
            Description = description
        };

        var (exitCode, scope) = await _shell.TryCallAsync(
            ct => _client.CreateScopeHandlerAsync(request, ct), cancellationToken);

        if (exitCode != 0)
        {
            return exitCode;
        }

        _shell.DisplaySuccess(scope!, _hostOptions.OutputFormat,
            s => $"Scope '{s.Dimension}' created (id: {s.Id}, version: {s.Version}).");
        return 0;
    }
}