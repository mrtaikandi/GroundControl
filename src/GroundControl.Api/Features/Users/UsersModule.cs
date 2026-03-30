using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Features.Users.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Users;

[RunsAfter<AppCommonModule>(Required = true)]
[RunsAfter<AuthenticationModule>(Required = true)]
internal sealed class UsersModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<CreateUserHandler>();
        builder.Services.AddTransient<GetUserHandler>();
        builder.Services.AddTransient<ListUsersHandler>();
        builder.Services.AddTransient<UpdateUserHandler>();
        builder.Services.AddTransient<DeleteUserHandler>();
        builder.Services.AddTransient<ChangePasswordHandler>();

        builder.Services.AddTransient<IAsyncValidator<CreateUserRequest>, CreateUserValidator>();
        builder.Services.AddTransient<IAsyncValidator<UpdateUserRequest>, UpdateUserValidator>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        CreateUserHandler.Endpoint(group);
        GetUserHandler.Endpoint(group);
        ListUsersHandler.Endpoint(group);
        UpdateUserHandler.Endpoint(group);
        DeleteUserHandler.Endpoint(group);
        ChangePasswordHandler.Endpoint(group);
    }
}