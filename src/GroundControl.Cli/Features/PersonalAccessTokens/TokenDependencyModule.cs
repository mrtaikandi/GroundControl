using Microsoft.Extensions.DependencyInjection;

namespace GroundControl.Cli.Features.PersonalAccessTokens;

internal sealed class TokenDependencyModule : IDependencyModule
{
    public void ConfigureServices(DependencyModuleContext context, IServiceCollection services)
    {
    }
}