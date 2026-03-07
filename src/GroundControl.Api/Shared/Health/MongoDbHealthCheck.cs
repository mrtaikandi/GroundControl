using System.Diagnostics.CodeAnalysis;
using GroundControl.Persistence.MongoDb;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GroundControl.Api.Shared.Health;

/// <summary>
/// A health check for Mongo Db databases.
/// </summary>
internal sealed class MongoDbHealthCheck(IMongoDbContext mongoDbContext) : IHealthCheck
{
    private static readonly BsonDocumentCommand<BsonDocument> Command = new(BsonDocument.Parse("{ping:1}"));

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to catch all exceptions to report them as health check failures.")]
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await mongoDbContext.Database.RunCommandAsync(Command, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
        {
            // Ignored.
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"MongoDb check failed with exception: {ex.ToShortString()}");
        }

        return HealthCheckResult.Healthy();
    }
}