using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Groups.Create;

internal sealed class CreateGroupHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CreateGroupOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public CreateGroupHandler(
        IShell shell,
        IOptions<CreateGroupOptions> options,
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
        var description = _options.Description;

        if (name is null && _hostOptions.NoInteractive)
        {
            _shell.DisplayError("Missing required option: --name. Provide it explicitly when using --no-interactive.");
            return 1;
        }

        if (name is null)
        {
            name = await _shell.PromptForStringAsync("Group name:", cancellationToken: cancellationToken);
        }

        if (description is null && !_hostOptions.NoInteractive)
        {
            description = await _shell.PromptForStringAsync("Description:", isOptional: true, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(description))
            {
                description = null;
            }
        }

        var request = new CreateGroupRequest
        {
            Name = name,
            Description = description
        };

        var (exitCode, group) = await _shell.TryCallAsync(
            ct => _client.CreateGroupHandlerAsync(request, ct), cancellationToken);

        if (exitCode != 0)
        {
            return exitCode;
        }

        _shell.DisplaySuccess($"Group '{group!.Name}' created (id: {group.Id}, version: {group.Version}).");
        return 0;
    }
}