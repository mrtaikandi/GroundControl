using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Variables;

[RunsAfter<ApplicationModule>(Required = true)]
[RunsAfter<AuthenticationModule>(Required = true)]
internal sealed class VariablesModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<CreateVariableHandler>();
        builder.Services.AddTransient<GetVariableHandler>();
        builder.Services.AddTransient<ListVariablesHandler>();
        builder.Services.AddTransient<UpdateVariableHandler>();
        builder.Services.AddTransient<DeleteVariableHandler>();

        builder.Services.AddTransient<IAsyncValidator<CreateVariableRequest>, CreateVariableValidator>();
        builder.Services.AddTransient<IAsyncValidator<UpdateVariableRequest>, UpdateVariableValidator>();
        builder.Services.AddTransient<DeleteVariableValidator>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/variables")
            .WithTags("Variables");

        CreateVariableHandler.Endpoint(group);
        GetVariableHandler.Endpoint(group);
        ListVariablesHandler.Endpoint(group);
        UpdateVariableHandler.Endpoint(group);
        DeleteVariableHandler.Endpoint(group);
    }
}