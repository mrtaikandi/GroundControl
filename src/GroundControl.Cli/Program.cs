using GroundControl.Api.Client;
using Microsoft.Extensions.DependencyInjection;

// When no command is specified, launch the TUI dashboard
if (args.Length == 0)
{
    args = ["tui"];
}

var builder = new CliHostBuilder(args, "GroundControl management tool");
builder.Services.AddGroundControlClient(options =>
    {
        var baseAddress = builder.Configuration["GroundControl:ServerUrl"] ?? "https://localhost:5001";
        options.BaseAddress = new Uri(baseAddress);
    })
    .AddStandardResilienceHandler();

await builder.RunAsync();