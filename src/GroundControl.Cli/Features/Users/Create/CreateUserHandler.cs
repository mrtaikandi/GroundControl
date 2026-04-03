using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Users.Create;

internal sealed class CreateUserHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CreateUserOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public CreateUserHandler(
        IShell shell,
        IOptions<CreateUserOptions> options,
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
        var username = _options.Username;
        var email = _options.Email;
        var password = _options.Password;

        if (_hostOptions.NoInteractive && (username is null || email is null))
        {
            var missing = new List<string>();
            if (username is null)
            {
                missing.Add("--username");
            }

            if (email is null)
            {
                missing.Add("--email");
            }

            _shell.DisplayError($"Missing required option(s): {string.Join(", ", missing)}. Provide them explicitly when using --no-interactive.");
            return 1;
        }

        if (username is null)
        {
            username = await _shell.PromptForStringAsync("Username:", cancellationToken: cancellationToken);
        }

        if (email is null)
        {
            email = await _shell.PromptForStringAsync("Email:", cancellationToken: cancellationToken);
        }

        if (password is null && !_hostOptions.NoInteractive)
        {
            password = await _shell.PromptForSecretAsync("Password:", cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(password))
            {
                password = null;
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

        try
        {
            var request = new CreateUserRequest
            {
                Username = username,
                Email = email,
                Password = password,
                Grants = grants
            };

            var user = await _client.CreateUserHandlerAsync(request, cancellationToken);
            _shell.DisplaySuccess($"User '{user.Username}' created (id: {user.Id}, version: {user.Version}).");
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