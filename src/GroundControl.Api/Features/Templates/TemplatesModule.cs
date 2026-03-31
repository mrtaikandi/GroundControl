using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Templates;

internal sealed class TemplatesModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<CreateTemplateHandler>();
        builder.Services.AddTransient<GetTemplateHandler>();
        builder.Services.AddTransient<ListTemplatesHandler>();
        builder.Services.AddTransient<UpdateTemplateHandler>();
        builder.Services.AddTransient<DeleteTemplateHandler>();

        builder.Services.AddTransient<IAsyncValidator<CreateTemplateRequest>, CreateTemplateValidator>();
        builder.Services.AddTransient<IAsyncValidator<UpdateTemplateRequest>, UpdateTemplateValidator>();
        builder.Services.AddTransient<DeleteTemplateValidator>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/templates")
            .WithTags("Templates");

        CreateTemplateHandler.Endpoint(group);
        GetTemplateHandler.Endpoint(group);
        ListTemplatesHandler.Endpoint(group);
        UpdateTemplateHandler.Endpoint(group);
        DeleteTemplateHandler.Endpoint(group);
    }
}