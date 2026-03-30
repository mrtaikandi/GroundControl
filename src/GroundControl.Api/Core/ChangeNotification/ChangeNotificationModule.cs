using GroundControl.Host.Api;

namespace GroundControl.Api.Core.ChangeNotification;

[RunsAfter<AppCommonModule>(Required = true)]
internal sealed class ChangeNotificationModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        var changeNotifierMode = builder.Configuration.GetValue<string>("ChangeNotifier:Mode");
        if (string.Equals(changeNotifierMode, "MongoChangeStream", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<MongoChangeStreamNotifier>();
            builder.Services.AddSingleton<IChangeNotifier>(sp => sp.GetRequiredService<MongoChangeStreamNotifier>());
            builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<MongoChangeStreamNotifier>());
        }
        else
        {
            builder.Services.AddSingleton<IChangeNotifier, InProcessChangeNotifier>();
        }
    }
}