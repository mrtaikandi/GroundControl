using GroundControl.Persistence.MongoDb.Conventions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GroundControl.Persistence.MongoDb;

/// <summary>
/// Configures MongoDB document indexes during application startup.
/// </summary>
public sealed class MongoIndexSetupService : IHostedService
{
    private readonly ILogger<MongoIndexSetupService> _logger;
    private readonly IDocumentConfiguration[] _documentConfigurations;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoIndexSetupService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="documentConfigurations">The registered document configurations.</param>
    public MongoIndexSetupService(ILogger<MongoIndexSetupService> logger, IEnumerable<IDocumentConfiguration> documentConfigurations)
    {
        ArgumentNullException.ThrowIfNull(documentConfigurations);

        _documentConfigurations = documentConfigurations.ToArray();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs all registered MongoDB document configurations.
    /// </summary>
    /// <param name="cancellationToken">The startup cancellation token.</param>
    /// <returns>A task that completes when configuration finishes.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogIndexSetupStarting(_documentConfigurations.Length);

        foreach (var documentConfiguration in _documentConfigurations)
        {
            await documentConfiguration.ConfigureAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogIndexSetupCompleted(_documentConfigurations.Length);
    }

    /// <summary>
    /// Stops the startup service.
    /// </summary>
    /// <param name="cancellationToken">The shutdown cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static partial class LoggingExtensions
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Starting MongoDB index setup for {DocumentConfigurationCount} document configurations.")]
    public static partial void LogIndexSetupStarting(this ILogger<MongoIndexSetupService> logger, int documentConfigurationCount);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Completed MongoDB index setup for {DocumentConfigurationCount} document configurations.")]
    public static partial void LogIndexSetupCompleted(this ILogger<MongoIndexSetupService> logger, int documentConfigurationCount);
}