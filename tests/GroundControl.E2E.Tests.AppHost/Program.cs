var builder = DistributedApplication.CreateBuilder(args);

// MongoDB with single-node replica set (required for change streams).
// Uses AddContainer instead of AddMongoDB to avoid Aspire's auto-generated auth,
// which requires a keyFile for replica sets. No auth needed for E2E tests.
// The init script initializes the RS via /docker-entrypoint-initdb.d/ on first start.
// The member hostname is localhost:27017 (container-internal); clients use
// directConnection=true to bypass topology discovery via the randomized host port.
var initScript = Path.Combine(AppContext.BaseDirectory, "mongo-init.js");
var mongodb = builder.AddContainer("mongodb", "mongo", "8")
    .WithArgs("--replSet", "rs0", "--bind_ip_all")
    .WithEndpoint(targetPort: 27017, scheme: "tcp", name: "tcp")
    .WithBindMount(initScript, "/docker-entrypoint-initdb.d/mongo-init.js", isReadOnly: true);

// GroundControl API — the connection string must use directConnection=true because the
// RS member hostname (localhost:27017) is container-internal and not reachable from the host.
var mongoEndpoint = mongodb.GetEndpoint("tcp");
builder.AddProject<Projects.GroundControl_Api>("api")
    .WaitFor(mongodb)
    .WithEnvironment(ctx =>
    {
        ctx.EnvironmentVariables["ConnectionStrings__Storage"] =
            ReferenceExpression.Create($"mongodb://localhost:{mongoEndpoint.Property(EndpointProperty.Port)}/?directConnection=true");
    })
    .WithEnvironment("Persistence__MongoDb__DatabaseName", "groundcontrol_e2e")
    .WithEnvironment("Authentication__AuthenticationMode", "None")
    .WithEnvironment("DataProtection__Mode", "FileSystem")
    .WithEnvironment("ChangeNotifier__Mode", "InProcess")
    .WithHttpHealthCheck("/healthz/ready");

builder.Build().Run();