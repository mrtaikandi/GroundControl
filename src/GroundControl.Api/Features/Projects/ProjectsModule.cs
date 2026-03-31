using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Projects;

internal sealed class ProjectsModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<CreateProjectHandler>();
        builder.Services.AddTransient<GetProjectHandler>();
        builder.Services.AddTransient<ListProjectsHandler>();
        builder.Services.AddTransient<UpdateProjectHandler>();
        builder.Services.AddTransient<DeleteProjectHandler>();
        builder.Services.AddTransient<AddProjectTemplateHandler>();
        builder.Services.AddTransient<RemoveProjectTemplateHandler>();

        builder.Services.AddTransient<IAsyncValidator<CreateProjectRequest>, CreateProjectValidator>();
        builder.Services.AddTransient<IAsyncValidator<UpdateProjectRequest>, UpdateProjectValidator>();
        builder.Services.AddTransient<AddProjectTemplateValidator>();
        builder.Services.AddTransient<DeleteProjectValidator>();
        builder.Services.AddTransient<RemoveProjectTemplateValidator>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/projects")
            .WithTags("Projects");

        CreateProjectHandler.Endpoint(group);
        GetProjectHandler.Endpoint(group);
        ListProjectsHandler.Endpoint(group);
        UpdateProjectHandler.Endpoint(group);
        DeleteProjectHandler.Endpoint(group);
        AddProjectTemplateHandler.Endpoint(group);
        RemoveProjectTemplateHandler.Endpoint(group);
    }
}