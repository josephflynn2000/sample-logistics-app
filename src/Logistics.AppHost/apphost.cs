#:package Aspire.Hosting.JavaScript@13.6.4
#:package Aspire.Hosting.PostgreSQL@13.6.4
#:package Aspire.Hosting.Redis@13.6.4
#:sdk Aspire.AppHost.Sdk@13.6.4
#:property AspireUseCliBundle=true
#:project ../Logistics.Backend/Logistics.Backend.csproj

using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.Logistics_Backend>("Backend")
    .WithHttpHealthCheck("/health");

#pragma warning disable ASPIREJAVASCRIPT001
var frontend = builder.AddViteApp("Frontend", "../..", runScriptName: "start")
    .PublishAsNodeServer(entryPoint: "dist/Logistics.Frontend/server/server.mjs", outputPath: "dist/Logistics.Frontend")
    .WithReference(backend)
    .WaitFor(backend)
    .WithExternalHttpEndpoints();
#pragma warning restore ASPIREJAVASCRIPT001

if (!builder.Environment.IsDevelopment())
{
    var postgres = builder.AddPostgres("postgres");
    var postgresdb = postgres.AddDatabase("LogisticsDatabase");

    backend.WithReference(postgresdb)
        .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
        .WaitFor(postgresdb);

    var sessionCache = builder.AddRedis("session-cache");

    frontend.WithReference(sessionCache)
        .WaitFor(sessionCache);
}

builder.Build().Run();
