using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using GroundControl.Cli.Shared.Parsing;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Variables.Create;

internal sealed class CreateVariableHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CreateVariableOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public CreateVariableHandler(
        IShell shell,
        IOptions<CreateVariableOptions> options,
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
        var scope = _options.Scope;

        if (_hostOptions.NoInteractive && (name is null || scope is null))
        {
            var missing = new List<string>();
            if (name is null)
            {
                missing.Add("--name");
            }

            if (scope is null)
            {
                missing.Add("--scope");
            }

            _shell.DisplayError($"Missing required option(s): {string.Join(", ", missing)}. Provide them explicitly when using --no-interactive.");
            return 1;
        }

        if (name is null)
        {
            name = await _shell.PromptForStringAsync("Variable name:", cancellationToken: cancellationToken);
        }

        if (scope is null)
        {
            var scopeStr = await _shell.PromptForStringAsync("Scope (Global or Project):", cancellationToken: cancellationToken);
            scope = Enum.Parse<VariableScope>(scopeStr, ignoreCase: true);
        }

        var sensitive = _options.Sensitive;
        if (sensitive is null && !_hostOptions.NoInteractive)
        {
            sensitive = await _shell.ConfirmAsync("Is this a sensitive value?", defaultValue: false, cancellationToken);
        }

        var description = _options.Description;
        if (description is null && !_hostOptions.NoInteractive)
        {
            description = await _shell.PromptForStringAsync("Description:", isOptional: true, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(description))
            {
                description = null;
            }
        }

        List<ScopedValueParser.ParsedScopedValue> parsedValues;
        try
        {
            parsedValues = ScopedValueParser.Parse(_options.Values, _options.ValuesJson);
        }
        catch (FormatException ex)
        {
            _shell.DisplayError(ex.Message);
            return 1;
        }

        if (parsedValues.Count == 0 && !_hostOptions.NoInteractive)
        {
            parsedValues = await PromptForScopedValuesAsync(cancellationToken);
        }

        var scopedValues = parsedValues.Select(v => new ScopedValueRequest
        {
            Scopes = v.Scopes.Count > 0 ? new Dictionary<string, string>(v.Scopes) : null,
            Value = v.Value
        }).ToList();

        var request = new CreateVariableRequest
        {
            Name = name,
            Scope = scope.Value,
            GroupId = _options.GroupId,
            ProjectId = _options.ProjectId,
            Values = scopedValues,
            IsSensitive = sensitive,
            Description = description
        };

        var (exitCode, variable) = await _shell.TryCallAsync(
            ct => _client.CreateVariableHandlerAsync(request, ct), cancellationToken);

        if (exitCode != 0)
        {
            return exitCode;
        }

        _shell.DisplaySuccess($"Variable '{variable!.Name}' created (id: {variable.Id}, version: {variable.Version}).");
        return 0;
    }

    private async Task<List<ScopedValueParser.ParsedScopedValue>> PromptForScopedValuesAsync(CancellationToken cancellationToken)
    {
        var values = new List<ScopedValueParser.ParsedScopedValue>();

        while (true)
        {
            var input = await _shell.PromptForStringAsync(
                "Scoped value (e.g., \"default=myval\" or \"env:prod=prodval\", empty to finish):",
                isOptional: true,
                cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(input))
            {
                break;
            }

            try
            {
                values.Add(ScopedValueParser.ParseSingle(input));
            }
            catch (FormatException ex)
            {
                _shell.DisplayError(ex.Message);
            }
        }

        return values;
    }
}