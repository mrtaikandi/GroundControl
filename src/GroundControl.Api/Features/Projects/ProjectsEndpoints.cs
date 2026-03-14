using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Shared.Validation;

namespace GroundControl.Api.Features.Projects;

internal static class ProjectsEndpoints
{
    public static IServiceCollection AddProjectsHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<CreateProjectHandler>();
        services.AddTransient<GetProjectHandler>();
        services.AddTransient<ListProjectsHandler>();
        services.AddTransient<UpdateProjectHandler>();
        services.AddTransient<DeleteProjectHandler>();
        services.AddTransient<AddProjectTemplateHandler>();
        services.AddTransient<RemoveProjectTemplateHandler>();

        services.AddTransient<IAsyncValidator<CreateProjectRequest>, CreateProjectValidator>();
        services.AddTransient<IAsyncValidator<UpdateProjectRequest>, UpdateProjectValidator>();
        services.AddTransient<AddProjectTemplateValidator>();
        services.AddTransient<DeleteProjectValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/projects")
            .WithTags("Projects");

        CreateProjectHandler.Endpoint(group);
        GetProjectHandler.Endpoint(group);
        ListProjectsHandler.Endpoint(group);
        UpdateProjectHandler.Endpoint(group);
        DeleteProjectHandler.Endpoint(group);
        AddProjectTemplateHandler.Endpoint(group);
        RemoveProjectTemplateHandler.Endpoint(group);

        return endpoints;
    }
}