using System.CommandLine;

namespace GroundControl.Cli.Features.Auth.Login;

internal sealed class LoginCommand : Command<LoginHandler, LoginOptions>
{
    public LoginCommand()
        : base("login", "Log in to a GroundControl server")
    {
        var serverUrlOption = new Option<string?>("--server-url") { Description = "The server URL (Required)" };
        var methodOption = new Option<AuthMethod?>("--method") { Description = "Authentication method (None, Pat, ApiKey, Credentials) (Required)" };
        var tokenOption = new Option<string?>("--token") { Description = "Personal access token" };
        var clientIdOption = new Option<string?>("--client-id") { Description = "API key client ID" };
        var clientSecretOption = new Option<string?>("--client-secret") { Description = "API key client secret" };
        var usernameOption = new Option<string?>("--username") { Description = "Username for credential auth" };
        var passwordOption = new Option<string?>("--password") { Description = "Password for credential auth" };

        Options.Add(serverUrlOption);
        Options.Add(methodOption);
        Options.Add(tokenOption);
        Options.Add(clientIdOption);
        Options.Add(clientSecretOption);
        Options.Add(usernameOption);
        Options.Add(passwordOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.ServerUrl = parseResult.GetValue(serverUrlOption);
            options.Method = parseResult.GetValue(methodOption);
            options.Token = parseResult.GetValue(tokenOption);
            options.ClientId = parseResult.GetValue(clientIdOption);
            options.ClientSecret = parseResult.GetValue(clientSecretOption);
            options.Username = parseResult.GetValue(usernameOption);
            options.Password = parseResult.GetValue(passwordOption);
        });
    }
}