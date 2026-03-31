using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Groups;

internal sealed class GroupsModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<CreateGroupHandler>();
        builder.Services.AddTransient<GetGroupHandler>();
        builder.Services.AddTransient<ListGroupsHandler>();
        builder.Services.AddTransient<UpdateGroupHandler>();
        builder.Services.AddTransient<DeleteGroupHandler>();

        builder.Services.AddTransient<IAsyncValidator<CreateGroupRequest>, CreateGroupValidator>();
        builder.Services.AddTransient<IAsyncValidator<UpdateGroupRequest>, UpdateGroupValidator>();
        builder.Services.AddTransient<DeleteGroupValidator>();

        builder.Services.AddTransient<ListGroupMembersHandler>();
        builder.Services.AddTransient<SetGroupMemberHandler>();
        builder.Services.AddTransient<RemoveGroupMemberHandler>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/groups")
            .WithTags("Groups");

        CreateGroupHandler.Endpoint(group);
        GetGroupHandler.Endpoint(group);
        ListGroupsHandler.Endpoint(group);
        UpdateGroupHandler.Endpoint(group);
        DeleteGroupHandler.Endpoint(group);

        ListGroupMembersHandler.Endpoint(group);
        SetGroupMemberHandler.Endpoint(group);
        RemoveGroupMemberHandler.Endpoint(group);
    }
}