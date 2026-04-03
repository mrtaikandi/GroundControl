using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using GroundControl.Cli.Shared.Parsing;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.Variables.Update;

internal sealed class UpdateVariableHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly UpdateVariableOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public UpdateVariableHandler(
        IShell shell,
        IOptions<UpdateVariableOptions> options,
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
                var current = await _client.GetVariableHandlerAsync(_options.Id, decrypt: null, cancellationToken);
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

        var request = new UpdateVariableRequest
        {
            Values = scopedValues!,
            IsSensitive = _options.Sensitive,
            Description = _options.Description
        };

        try
        {
            GroundControlClient.SetIfMatch(version.Value);
            var variable = await _client.UpdateVariableHandlerAsync(_options.Id, request, cancellationToken);
            _shell.DisplaySuccess($"Variable '{variable.Name}' updated (version: {variable.Version}).");
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex) when (ex.StatusCode == 409)
        {
            var retried = await _shell.HandleConflictAsync(
                async ct =>
                {
                    var current = await _client.GetVariableHandlerAsync(_options.Id, decrypt: null, ct);
                    var diffs = new List<FieldDiff>();

                    if (_options.Description is not null && _options.Description != (current.Description ?? string.Empty))
                    {
                        diffs.Add(new FieldDiff("Description", _options.Description, current.Description ?? string.Empty));
                    }

                    return new ConflictInfo(current.Version, diffs);
                },
                async (newVersion, ct) =>
                {
                    GroundControlClient.SetIfMatch(newVersion);
                    var variable = await _client.UpdateVariableHandlerAsync(_options.Id, request, ct);
                    _shell.DisplaySuccess($"Variable '{variable.Name}' updated (version: {variable.Version}).");
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
}