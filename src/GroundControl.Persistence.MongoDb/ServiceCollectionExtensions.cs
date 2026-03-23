using GroundControl.Persistence.MongoDb.Conventions;
using GroundControl.Persistence.MongoDb.Stores;
using GroundControl.Persistence.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb;

/// <summary>
/// Provides dependency injection registration helpers for the MongoDB persistence layer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers GroundControl MongoDB infrastructure services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddGroundControlMongo(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidateOptions<MongoDbOptions>, MongoDbOptions.Validator>();
        services.AddOptions<MongoDbOptions>()
            .Configure<IConfiguration>((option, config) =>
            {
                config.GetSection(MongoDbOptions.SectionName).Bind(option);
                if (string.IsNullOrWhiteSpace(option.ConnectionString))
                {
                    option.ConnectionString = config.GetConnectionString(option.ConnectionStringKey);
                }
            })
            .Validate(option => !string.IsNullOrWhiteSpace(option.ConnectionString), "MongoDb connection string is not set.");

        services.AddSingleton<IMongoDbContext, MongoDbContext>();
        services.AddSingleton<IMongoClient>(sp =>
        {
            MongoConventions.Register();

            var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                var config = sp.GetRequiredService<IConfiguration>();
                options.ConnectionString = config.GetConnectionString(options.ConnectionStringKey);
            }

            return new MongoClient(options.ConnectionString);
        });

        services.TryAddEnumerable([
            ServiceDescriptor.Singleton<IDocumentConfiguration, ScopeConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, GroupConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, RoleConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, TemplateConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, ProjectConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, ConfigEntryConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, VariableConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, SnapshotConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, ClientConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, UserConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, RefreshTokenConfiguration>(),
            ServiceDescriptor.Singleton<IDocumentConfiguration, PersonalAccessTokenConfiguration>(),
        ]);

        services.AddHostedService<MongoIndexSetupService>();
        services.TryAddSingleton<IMongoDbContext, MongoDbContext>();

        services.TryAddSingleton<IRoleStore, RoleStore>();
        services.TryAddSingleton<IScopeStore, ScopeStore>();
        services.TryAddSingleton<IGroupStore, GroupStore>();
        services.TryAddSingleton<IClientStore, ClientStore>();
        services.TryAddSingleton<IProjectStore, ProjectStore>();
        services.TryAddSingleton<IVariableStore, VariableStore>();
        services.TryAddSingleton<ITemplateStore, TemplateStore>();
        services.TryAddSingleton<ISnapshotStore, MongoSnapshotStore>();
        services.TryAddSingleton<IConfigEntryStore, ConfigEntryStore>();
        services.TryAddSingleton<IUserStore, UserStore>();
        services.TryAddSingleton<IRefreshTokenStore, RefreshTokenStore>();
        services.TryAddSingleton<IPersonalAccessTokenStore, PersonalAccessTokenStore>();

        return services;
    }
}