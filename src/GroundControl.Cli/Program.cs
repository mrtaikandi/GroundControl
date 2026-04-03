using GroundControl.Api.Client;
using Microsoft.Extensions.DependencyInjection;

var builder = new CliHostBuilder(args, "GroundControl management tool");
builder.Services.AddGroundControlClient(options =>
    {
        var baseAddress = builder.Configuration["GroundControl:ServerUrl"] ?? "https://localhost:5001";
        options.BaseAddress = new Uri(baseAddress);
    })
    .AddStandardResilienceHandler();

await builder.RunAsync();