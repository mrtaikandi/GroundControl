var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("groundcontrol-mongo-data")
    .WithLifetime(ContainerLifetime.Persistent);

var mongodb = mongo.AddDatabase("Storage", databaseName: "GroundControl");

// GroundControl API
builder.AddProject<Projects.GroundControl_Api>("api")
    .WaitFor(mongodb)
    .WithReference(mongodb)
    .WithEnvironment("Authentication__Mode", "None")
    .WithEnvironment("DataProtection__Mode", "FileSystem")
    .WithHttpHealthCheck("/healthz/ready", endpointName: "http");

builder.AddProject<Projects.GroundControl_Samples_LinkConsole>("link-console");

builder.Build().Run();