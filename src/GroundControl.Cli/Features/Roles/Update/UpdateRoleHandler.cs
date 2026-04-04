using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.Roles.Update;

internal sealed class UpdateRoleHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly UpdateRoleOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public UpdateRoleHandler(
        IShell shell,
        IOptions<UpdateRoleOptions> options,
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
            ct => _client.GetRoleHandlerAsync(_options.Id, ct),
            r => r.Version,
            cancellationToken);

        if (!resolved.IsSuccess)
        {
            return resolved.ExitCode;
        }

        var version = resolved.Version;

        var permissions = _options.Permissions?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // WhenWritingNull serializer policy omits null fields from the JSON body,
        // so only fields the user explicitly provided are sent to the API.
        var request = new UpdateRoleRequest
        {
            Name = _options.Name!,
            Permissions = permissions!,
            Description = _options.Description
        };

        return await _shell.TryCallWithConflictHandlingAsync(
            _hostOptions.NoInteractive,
            async ct =>
            {
                GroundControlClient.SetIfMatch(version);
                var role = await _client.UpdateRoleHandlerAsync(_options.Id, request, ct);
                _shell.DisplaySuccess($"Role '{role.Name}' updated (version: {role.Version}).");
                return 0;
            },
            async ct =>
            {
                var current = await _client.GetRoleHandlerAsync(_options.Id, ct);
                var diffs = new List<FieldDiff>();

                if (_options.Name is not null && _options.Name != current.Name)
                {
                    diffs.Add(new FieldDiff("Name", _options.Name, current.Name));
                }

                if (permissions is not null)
                {
                    var currentPerms = string.Join(", ", current.Permissions);
                    var requestedPerms = string.Join(", ", permissions);
                    if (requestedPerms != currentPerms)
                    {
                        diffs.Add(new FieldDiff("Permissions", requestedPerms, currentPerms));
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
                var role = await _client.UpdateRoleHandlerAsync(_options.Id, request, ct);
                _shell.DisplaySuccess($"Role '{role.Name}' updated (version: {role.Version}).");
            },
            cancellationToken);
    }
}