using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Roles.Create;

internal sealed class CreateRoleHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CreateRoleOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public CreateRoleHandler(
        IShell shell,
        IOptions<CreateRoleOptions> options,
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

        if (_hostOptions.NoInteractive && name is null)
        {
            _shell.DisplayError("Missing required option(s): --name. Provide them explicitly when using --no-interactive.");
            return 1;
        }

        if (name is null)
        {
            name = await _shell.PromptForStringAsync("Role name:", cancellationToken: cancellationToken);
        }

        string[] permissions;
        if (_options.Permissions is not null)
        {
            permissions = _options.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        else if (!_hostOptions.NoInteractive)
        {
            var selected = await _shell.PromptForMultiSelectionAsync(
                "Select permissions:",
                KnownPermissions.All,
                p => p,
                selectedChoices: [],
                cancellationToken: cancellationToken);

            permissions = [.. selected];
        }
        else
        {
            permissions = [];
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

        try
        {
            var request = new CreateRoleRequest
            {
                Name = name,
                Permissions = permissions,
                Description = description
            };

            var role = await _client.CreateRoleHandlerAsync(request, cancellationToken);
            _shell.DisplaySuccess($"Role '{role.Name}' created (id: {role.Id}, version: {role.Version}).");
            return 0;
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