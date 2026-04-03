using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.Users.Update;

internal sealed class UpdateUserHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly UpdateUserOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public UpdateUserHandler(
        IShell shell,
        IOptions<UpdateUserOptions> options,
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
                var current = await _client.GetUserHandlerAsync(_options.Id, cancellationToken);
                version = current.Version;
            }
            catch (GroundControlApiClientException<ProblemDetails> ex)
            {
                _shell.RenderProblemDetails(ex.Result);
                return 1;
            }
        }

        var (grants, invalidGrants) = GrantParser.Parse(_options.Grants);
        if (invalidGrants is not null)
        {
            foreach (var invalid in invalidGrants)
            {
                _shell.DisplayError($"Invalid role ID: '{invalid}'. Expected a valid GUID.");
            }

            return 1;
        }

        // WhenWritingNull serializer policy omits null fields from the JSON body,
        // so only fields the user explicitly provided are sent to the API.
        var request = new UpdateUserRequest
        {
            Username = _options.Username!,
            Email = _options.Email!,
            IsActive = _options.IsActive,
            Grants = grants
        };

        try
        {
            GroundControlClient.SetIfMatch(version.Value);
            var user = await _client.UpdateUserHandlerAsync(_options.Id, request, cancellationToken);
            _shell.DisplaySuccess($"User '{user.Username}' updated (version: {user.Version}).");
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex) when (ex.StatusCode == 409)
        {
            var retried = await _shell.HandleConflictAsync(
                async ct =>
                {
                    var current = await _client.GetUserHandlerAsync(_options.Id, ct);
                    var diffs = new List<FieldDiff>();

                    if (_options.Username is not null && _options.Username != current.Username)
                    {
                        diffs.Add(new FieldDiff("Username", _options.Username, current.Username));
                    }

                    if (_options.Email is not null && _options.Email != current.Email)
                    {
                        diffs.Add(new FieldDiff("Email", _options.Email, current.Email));
                    }

                    if (_options.IsActive is not null && _options.IsActive != current.IsActive)
                    {
                        diffs.Add(new FieldDiff("Active", _options.IsActive.Value.ToString(), current.IsActive.ToString()));
                    }

                    return new ConflictInfo(current.Version, diffs);
                },
                async (newVersion, ct) =>
                {
                    GroundControlClient.SetIfMatch(newVersion);
                    var user = await _client.UpdateUserHandlerAsync(_options.Id, request, ct);
                    _shell.DisplaySuccess($"User '{user.Username}' updated (version: {user.Version}).");
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