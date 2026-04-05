using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.Users.Delete;

internal sealed class DeleteUserHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly DeleteUserOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public DeleteUserHandler(
        IShell shell,
        IOptions<DeleteUserOptions> options,
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
        var resolved = await _shell.ResolveVersionAsync(
            _options.Version,
            ct => _client.GetUserHandlerAsync(_options.Id, ct),
            r => r.Version,
            cancellationToken);

        if (!resolved.IsSuccess)
        {
            return resolved.ExitCode;
        }

        var version = resolved.Version;
        var current = resolved.Entity;

        if (!_options.Yes && !_hostOptions.NoInteractive)
        {
            var name = current?.Username ?? _options.Id.ToString();
            var confirmed = await _shell.ConfirmAsync(
                $"Delete user '{name}'?", defaultValue: false, cancellationToken);

            if (!confirmed)
            {
                _shell.DisplaySubtleMessage("Delete cancelled.");
                return 0;
            }
        }

        return await _shell.TryCallWithConflictHandlingAsync(
            _hostOptions.NoInteractive,
            async ct =>
            {
                GroundControlClient.SetIfMatch(version);
                await _client.DeleteUserHandlerAsync(_options.Id, ct);
                _shell.DisplaySuccess("User deleted.", _hostOptions.OutputFormat);
                return 0;
            },
            async ct =>
            {
                var latest = await _client.GetUserHandlerAsync(_options.Id, ct);
                var diffs = new List<FieldDiff>();

                if (current is not null)
                {
                    if (current.Username != latest.Username)
                    {
                        diffs.Add(new FieldDiff("Username", current.Username, latest.Username));
                    }

                    if (current.Email != latest.Email)
                    {
                        diffs.Add(new FieldDiff("Email", current.Email, latest.Email));
                    }

                    if (current.IsActive != latest.IsActive)
                    {
                        diffs.Add(new FieldDiff("Active", current.IsActive.ToString(), latest.IsActive.ToString()));
                    }
                }

                return new ConflictInfo(latest.Version, diffs);
            },
            async (newVersion, ct) =>
            {
                GroundControlClient.SetIfMatch(newVersion);
                await _client.DeleteUserHandlerAsync(_options.Id, ct);
                _shell.DisplaySuccess("User deleted.", _hostOptions.OutputFormat);
            },
            cancellationToken);
    }
}