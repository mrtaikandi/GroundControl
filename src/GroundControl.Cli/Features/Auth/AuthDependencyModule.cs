using GroundControl.Cli.Shared.Config;
using Microsoft.Extensions.DependencyInjection;

namespace GroundControl.Cli.Features.Auth;

internal sealed class AuthDependencyModule : IDependencyModule
{
    public void ConfigureServices(DependencyModuleContext context, IServiceCollection services)
    {
        services.AddSingleton(new CredentialStore(CredentialStore.DefaultPath));
    }
}