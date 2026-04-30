var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("groundcontrol-mongo-data")
    .WithLifetime(ContainerLifetime.Persistent);

var mongodb = mongo.AddDatabase("Storage", databaseName: "GroundControl");

// GroundControl API
var api = builder.AddProject<Projects.GroundControl_Api>("api")
    .WaitFor(mongodb)
    .WithReference(mongodb)
    .WithEnvironment("Authentication__Mode", "None")
    .WithEnvironment("DataProtection__Mode", "FileSystem")
    .WithHttpHealthCheck("/healthz/ready", endpointName: "http");

builder.AddNpmApp("tower", "../../src/GroundControl.Tower", "dev")
    .WithReference(api)
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.GroundControl_Samples_LinkConsole>("link-console");

builder.Build().Run();