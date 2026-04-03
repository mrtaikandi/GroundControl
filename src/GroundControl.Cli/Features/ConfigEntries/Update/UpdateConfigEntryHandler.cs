using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using GroundControl.Cli.Shared.Parsing;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.ConfigEntries.Update;

internal sealed class UpdateConfigEntryHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly UpdateConfigEntryOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public UpdateConfigEntryHandler(
        IShell shell,
        IOptions<UpdateConfigEntryOptions> options,
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
        var version = _options.Version;

        if (version is null)
        {
            try
            {
                var current = await _client.GetConfigEntryHandlerAsync(_options.Id, decrypt: null, cancellationToken);
                version = current.Version;
            }
            catch (GroundControlApiClientException<ProblemDetails> ex)
            {
                _shell.RenderProblemDetails(ex.Result);
                return 1;
            }
        }

        List<ScopedValueRequest>? scopedValues = null;
        if (_options.Values is not null || _options.ValuesJson is not null)
        {
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

            scopedValues = parsedValues.Select(v => new ScopedValueRequest
            {
                Scopes = v.Scopes.Count > 0 ? new Dictionary<string, string>(v.Scopes) : null,
                Value = v.Value
            }).ToList();
        }

        // WhenWritingNull serializer policy omits null fields from the JSON body,
        // so only fields the user explicitly provided are sent to the API.
        var request = new UpdateConfigEntryRequest
        {
            ValueType = _options.ValueType!,
            Values = scopedValues!,
            IsSensitive = _options.Sensitive,
            Description = _options.Description
        };

        try
        {
            GroundControlClient.SetIfMatch(version.Value);
            var entry = await _client.UpdateConfigEntryHandlerAsync(_options.Id, request, cancellationToken);
            _shell.DisplaySuccess($"Config entry '{entry.Key}' updated (version: {entry.Version}).");
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex) when (ex.StatusCode == 409)
        {
            var retried = await _shell.HandleConflictAsync(
                async ct =>
                {
                    var current = await _client.GetConfigEntryHandlerAsync(_options.Id, decrypt: null, ct);
                    var diffs = new List<FieldDiff>();

                    if (_options.ValueType is not null && _options.ValueType != current.ValueType)
                    {
                        diffs.Add(new FieldDiff("Value Type", _options.ValueType, current.ValueType));
                    }

                    if (scopedValues is not null)
                    {
                        var currentValuesStr = FormatScopedValues(current.Values);
                        var requestedValuesStr = FormatScopedValueRequests(scopedValues);
                        if (requestedValuesStr != currentValuesStr)
                        {
                            diffs.Add(new FieldDiff("Values", requestedValuesStr, currentValuesStr));
                        }
                    }

                    if (_options.Description is not null && _options.Description != (current.Description ?? string.Empty))
                    {
                        diffs.Add(new FieldDiff("Description", _options.Description, current.Description ?? string.Empty));
                    }

                    return new ConflictInfo(current.Version, diffs);
                },
                async (newVersion, ct) =>
                {
                    GroundControlClient.SetIfMatch(newVersion);
                    var entry = await _client.UpdateConfigEntryHandlerAsync(_options.Id, request, ct);
                    _shell.DisplaySuccess($"Config entry '{entry.Key}' updated (version: {entry.Version}).");
                },
                _hostOptions.NoInteractive,
                cancellationToken);

            return retried ? 0 : 1;
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

    private static string FormatScopedValues(ICollection<ScopedValue> values) =>
        string.Join("; ", values.Select(v =>
        {
            var scope = v.Scopes is { Count: > 0 }
                ? string.Join(",", v.Scopes.Select(s => $"{s.Key}:{s.Value}"))
                : "default";
            return $"{scope}={v.Value}";
        }));

    private static string FormatScopedValueRequests(ICollection<ScopedValueRequest> values) =>
        string.Join("; ", values.Select(v =>
        {
            var scope = v.Scopes is { Count: > 0 }
                ? string.Join(",", v.Scopes.Select(s => $"{s.Key}:{s.Value}"))
                : "default";
            return $"{scope}={v.Value}";
        }));
}