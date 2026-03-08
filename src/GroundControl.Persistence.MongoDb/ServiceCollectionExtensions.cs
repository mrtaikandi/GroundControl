using GroundControl.Persistence.MongoDb.Conventions;
using GroundControl.Persistence.MongoDb.Stores;
using GroundControl.Persistence.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

        services.TryAddSingleton<IMongoDbContext, MongoDbContext>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocumentConfiguration, ScopeConfiguration>());
        services.TryAddSingleton<IScopeStore, ScopeStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MongoIndexSetupService>());

        return services;
    }
}