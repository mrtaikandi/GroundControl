#:sdk Aspire.AppHost.Sdk@13.2.0
#:package Aspire.Hosting.MongoDB
#:project src/GroundControl.Api/GroundControl.Api.csproj

var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("groundcontrol-mongo-data")
    .WithLifetime(ContainerLifetime.Persistent);

var mongodb = mongo.AddDatabase("Storage", databaseName: "GroundControl");

// GroundControl API
builder.AddProject<Projects.GroundControl_Api>("api")
    .WaitFor(mongodb)
    .WithReference(mongodb)
    .WithEnvironment("Authentication__AuthenticationMode", "None")
    .WithEnvironment("DataProtection__Mode", "FileSystem")
    .WithHttpHealthCheck("/healthz/ready");

builder.Build().Run();