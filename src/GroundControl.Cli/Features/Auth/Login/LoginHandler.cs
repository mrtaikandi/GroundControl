using System.Text.Json.Nodes;
using GroundControl.Cli.Shared.Config;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Auth.Login;

internal sealed class LoginHandler : ICommandHandler
{
    private static readonly string[] MethodChoices = ["None", "Pat", "ApiKey", "Credentials"];

    private readonly IShell _shell;
    private readonly LoginOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly CredentialStore _store;

    public LoginHandler(
        IShell shell,
        IOptions<LoginOptions> options,
        IOptions<CliHostOptions> hostOptions,
        CredentialStore store)
    {
        _shell = shell;
        _options = options.Value;
        _hostOptions = hostOptions.Value;
        _store = store;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var serverUrl = _options.ServerUrl;
        var method = _options.Method;

        if (_hostOptions.NoInteractive)
        {
            return await HandleNonInteractiveAsync(serverUrl, method, cancellationToken);
        }

        if (serverUrl is null)
        {
            serverUrl = await _shell.PromptForStringAsync("Server URL:", cancellationToken: cancellationToken);
        }

        if (method is null)
        {
            var selected = await _shell.PromptForSelectionAsync("Authentication method:", MethodChoices, cancellationToken: cancellationToken);
            method = Enum.Parse<AuthMethod>(selected, ignoreCase: true);
        }

        return await CollectCredentialsAndSaveAsync(serverUrl, method.Value, cancellationToken);
    }

    private async Task<int> HandleNonInteractiveAsync(string? serverUrl, AuthMethod? method, CancellationToken cancellationToken)
    {
        List<string> missing = [];

        if (serverUrl is null)
        {
            missing.Add("--server-url");
        }

        if (method is null)
        {
            missing.Add("--method");
        }

        if (method is AuthMethod.Pat && _options.Token is null)
        {
            missing.Add("--token");
        }

        if (method is AuthMethod.ApiKey)
        {
            if (_options.ClientId is null)
            {
                missing.Add("--client-id");
            }

            if (_options.ClientSecret is null)
            {
                missing.Add("--client-secret");
            }
        }

        if (method is AuthMethod.Credentials)
        {
            if (_options.Username is null)
            {
                missing.Add("--username");
            }

            if (_options.Password is null)
            {
                missing.Add("--password");
            }
        }

        if (missing.Count > 0)
        {
            _shell.DisplayError($"Missing required options: {string.Join(", ", missing)}. Provide them explicitly when using --no-interactive.");
            return 1;
        }

        return await CollectCredentialsAndSaveAsync(serverUrl!, method!.Value, cancellationToken);
    }

    private async Task<int> CollectCredentialsAndSaveAsync(string serverUrl, AuthMethod method, CancellationToken cancellationToken)
    {
        var section = new JsonObject { ["ServerUrl"] = serverUrl };

        switch (method)
        {
            case AuthMethod.None:
                break;

            case AuthMethod.Pat:
            {
                var token = _options.Token;

                if (token is null)
                {
                    token = await _shell.PromptForSecretAsync("Personal access token:", cancellationToken: cancellationToken);
                }

                section["Auth"] = new JsonObject
                {
                    ["Method"] = "Bearer",
                    ["Token"] = token
                };

                break;
            }

            case AuthMethod.ApiKey:
            {
                var clientId = _options.ClientId;
                var clientSecret = _options.ClientSecret;

                if (clientId is null)
                {
                    clientId = await _shell.PromptForStringAsync("Client ID:", cancellationToken: cancellationToken);
                }

                if (clientSecret is null)
                {
                    clientSecret = await _shell.PromptForSecretAsync("Client secret:", cancellationToken: cancellationToken);
                }

                section["Auth"] = new JsonObject
                {
                    ["Method"] = "ApiKey",
                    ["ClientId"] = clientId,
                    ["ClientSecret"] = clientSecret
                };

                break;
            }

            case AuthMethod.Credentials:
            {
                var username = _options.Username;
                var password = _options.Password;

                if (username is null)
                {
                    username = await _shell.PromptForStringAsync("Username:", cancellationToken: cancellationToken);
                }

                if (password is null)
                {
                    password = await _shell.PromptForSecretAsync("Password:", cancellationToken: cancellationToken);
                }

                section["Auth"] = new JsonObject
                {
                    ["Method"] = "Credentials",
                    ["Username"] = username,
                    ["Password"] = password
                };

                break;
            }
        }

        await _store.WriteAsync(section, cancellationToken);
        _shell.DisplaySuccess($"Logged in to {serverUrl} using {method} authentication.");

        return 0;
    }
}