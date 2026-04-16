using GroundControl.Link;
using GroundControl.Samples.LinkConsole;

var builder = Host.CreateApplicationBuilder(args);

// Phase 1: Register GroundControl as a configuration source.
// Options are bound from appsettings.json / environment variables / user secrets.
var gcSection = builder.Configuration.GetSection("GroundControl");
builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl = new Uri(gcSection["ServerUrl"]!);
    options.ClientId = gcSection["ClientId"]!;
    options.ClientSecret = gcSection["ClientSecret"]!;

    if (Enum.TryParse<ConnectionMode>(gcSection["ConnectionMode"], ignoreCase: true, out var mode))
    {
        options.ConnectionMode = mode;
    }
});

// Phase 2: Register background services (SSE/polling), health check, and metrics.
builder.Services.AddGroundControl(builder.Configuration);

// Bind strongly-typed settings from the "Sample" section delivered by GroundControl.
builder.Services.Configure<SampleSettings>(builder.Configuration.GetSection("Sample"));

// Register the configuration monitor that logs changes to the console.
builder.Services.AddHostedService<ConfigurationMonitorService>();

var app = builder.Build();
app.Run();