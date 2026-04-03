using GroundControl.Cli.Features.Tui.Views;
using GroundControl.Cli.Shared.Config;
using Microsoft.Extensions.Configuration;
using Terminal.Gui.App;

namespace GroundControl.Cli.Features.Tui;

internal sealed class TuiCommandHandler : ICommandHandler
{
    private readonly IConfiguration _configuration;
    private readonly CredentialStore _credentialStore;

    public TuiCommandHandler(IConfiguration configuration, CredentialStore credentialStore)
    {
        _configuration = configuration;
        _credentialStore = credentialStore;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var serverUrl = _configuration["GroundControl:ServerUrl"] ?? "https://localhost:5001";
        var authMethod = await GetAuthMethodAsync(cancellationToken);

        using var app = Application.Create();
        app.Init();
        using var mainWindow = new MainWindow(app, serverUrl, authMethod);
        app.Run(mainWindow);

        return 0;
    }

    private async Task<string> GetAuthMethodAsync(CancellationToken cancellationToken)
    {
        var section = await _credentialStore.ReadAsync(cancellationToken);
        if (section is null)
        {
            return "None";
        }

        var auth = section["Auth"];
        if (auth is null)
        {
            return "None";
        }

        return auth["Method"]?.GetValue<string>() ?? "None";
    }
}