using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using GroundControl.Cli.Shared.Parsing;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.ConfigEntries.Create;

internal sealed class CreateConfigEntryHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CreateConfigEntryOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public CreateConfigEntryHandler(
        IShell shell,
        IOptions<CreateConfigEntryOptions> options,
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
        var key = _options.Key;
        var ownerId = _options.OwnerId;
        var ownerType = _options.OwnerType;
        var valueType = _options.ValueType;

        if (_hostOptions.NoInteractive && (key is null || ownerId is null || ownerType is null || valueType is null))
        {
            var missing = new List<string>();
            if (key is null)
            {
                missing.Add("--key");
            }

            if (ownerId is null)
            {
                missing.Add("--owner-id");
            }

            if (ownerType is null)
            {
                missing.Add("--owner-type");
            }

            if (valueType is null)
            {
                missing.Add("--value-type");
            }

            _shell.DisplayError($"Missing required option(s): {string.Join(", ", missing)}. Provide them explicitly when using --no-interactive.");
            return 1;
        }

        if (key is null)
        {
            key = await _shell.PromptForStringAsync("Configuration key:", cancellationToken: cancellationToken);
        }

        if (ownerId is null)
        {
            var ownerIdStr = await _shell.PromptForStringAsync("Owner ID:", cancellationToken: cancellationToken);
            ownerId = Guid.Parse(ownerIdStr);
        }

        if (ownerType is null)
        {
            var ownerTypeStr = await _shell.PromptForStringAsync("Owner type (Template or Project):", cancellationToken: cancellationToken);
            ownerType = Enum.Parse<ConfigEntryOwnerType>(ownerTypeStr, ignoreCase: true);
        }

        if (valueType is null)
        {
            valueType = await _shell.PromptForStringAsync("Value type (e.g., String, Int32, Boolean):", cancellationToken: cancellationToken);
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

        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = ownerId.Value,
            OwnerType = ownerType.Value,
            ValueType = valueType,
            Values = scopedValues,
            IsSensitive = sensitive,
            Description = description
        };

        var (exitCode, entry) = await _shell.TryCallAsync(
            ct => _client.CreateConfigEntryHandlerAsync(request, ct), cancellationToken);

        if (exitCode != 0)
        {
            return exitCode;
        }

        _shell.DisplaySuccess($"Config entry '{entry!.Key}' created (id: {entry.Id}, version: {entry.Version}).");
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