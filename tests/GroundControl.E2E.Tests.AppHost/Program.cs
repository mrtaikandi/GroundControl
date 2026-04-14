var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo");
var mongodb = mongo.AddDatabase("Storage", databaseName: "groundcontrol_e2e");

// GroundControl API
builder.AddProject<Projects.GroundControl_Api>("api")
    .WaitFor(mongodb)
    .WithReference(mongodb)
    .WithEnvironment("Authentication__AuthenticationMode", "None")
    .WithEnvironment("DataProtection__Mode", "FileSystem")
    .WithHttpHealthCheck("/healthz/ready");

builder.Build().Run();