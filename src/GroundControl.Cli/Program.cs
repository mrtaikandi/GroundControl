using GroundControl.Cli.Shared.ApiClient;
using GroundControl.Cli.Shared.Config;
using Microsoft.Extensions.DependencyInjection;

// When no command is specified, launch the TUI dashboard
if (args.Length == 0)
{
    args = ["tui"];
}

var builder = new CliHostBuilder(args, "GroundControl management tool");

new ApiClientModule().ConfigureServices(new DependencyModuleContext(builder.Environment, builder.Configuration), builder.Services);
builder.Services.AddSingleton(new CredentialStore(CredentialStore.DefaultPath));

await builder.RunAsync();