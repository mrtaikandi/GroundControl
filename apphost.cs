#:sdk Aspire.AppHost.Sdk@13.2.0
#:package MongoDB.Driver
#:project src/GroundControl.Api/GroundControl.Api.csproj

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = DistributedApplication.CreateBuilder(args);

// MongoDB with single-node replica set (required for change streams).
// Uses AddContainer instead of AddMongoDB to avoid Aspire's auto-generated auth,
// which requires a keyFile for replica sets. No auth needed for local development.
// Port fixed to 27017 so the replica set member hostname (localhost:27017)
// is resolvable from the host, enabling full topology discovery without directConnection.
const int MongoPort = 27017;
const string MongoHost = "localhost";
var mongodb = builder.AddContainer("mongodb", "mongo", "8")
    .WithArgs("--replSet", "rs0", "--bind_ip_all")
    .WithVolume("groundcontrol-mongodb-data", "/data/db")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpoint(port: MongoPort, targetPort: MongoPort, scheme: "tcp", name: "tcp");

// Health check that initializes the replica set on first run and verifies primary status.
const string HealthCheckName = "mongodb-replica-set";
builder.Services.AddHealthChecks()
    .Add(new HealthCheckRegistration(
        HealthCheckName,
        sp => new MongoReplicaSetHealthCheck(MongoHost, MongoPort),
        failureStatus: HealthStatus.Unhealthy,
        tags: null));

mongodb.WithHealthCheck(HealthCheckName);

// GroundControl API
var api = builder.AddProject<Projects.GroundControl_Api>("api")
    .WaitFor(mongodb)
    .WithEnvironment("ConnectionStrings__Storage", $"mongodb://{MongoHost}:{MongoPort}/?replicaSet=rs0")
    .WithEnvironment("Persistence__MongoDb__DatabaseName", "GroundControl")
    .WithEnvironment("Authentication__AuthenticationMode", "None")
    .WithEnvironment("DataProtection__Mode", "FileSystem")
    .WithEnvironment("ChangeNotifier__Mode", "InProcess")
    .WithHttpHealthCheck("/healthz/ready");

builder.Build().Run();

/// <summary>
/// Health check that initializes a single-node MongoDB replica set and verifies
/// the node is primary. On first run (empty volume), sends <c>replSetInitiate</c>.
/// On subsequent runs (persistent volume), catches AlreadyInitialized and verifies primary.
/// The <c>directConnection=true</c> is internal to this health check only.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Health check must catch all failures to report unhealthy status")]
internal sealed class MongoReplicaSetHealthCheck(string host, int port) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var memberHost = $"{host}:{port}";
            var settings = MongoClientSettings.FromConnectionString($"mongodb://{host}:{port}/?directConnection=true&serverSelectionTimeout=5000&connectTimeout=5000");
            using var client = new MongoClient(settings);
            var adminDb = client.GetDatabase("admin");

            try
            {
                var cmd = new BsonDocument
                {
                    {
                        "replSetInitiate", new BsonDocument
                        {
                            { "_id", "rs0" },
                            {
                                "members", new BsonArray
                                {
                                    new BsonDocument { { "_id", 0 }, { "host", memberHost } }
                                }
                            }
                        }
                    }
                };

                await adminDb.RunCommandAsync<BsonDocument>(cmd, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (MongoCommandException ex) when (ex.CodeName == "AlreadyInitialized")
            {
                // Persistent volume -- RS already configured
            }

            var hello = await adminDb.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: cancellationToken).ConfigureAwait(false);
            return hello.TryGetValue("isWritablePrimary", out var isPrimary) && isPrimary.AsBoolean
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("MongoDB replica set member is not yet primary.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"MongoDB not ready: {ex.Message}");
        }
    }
}