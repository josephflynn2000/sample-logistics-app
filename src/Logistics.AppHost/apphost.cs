#:package Aspire.Hosting.JavaScript@13.6.4
#:package Aspire.Hosting.PostgreSQL@13.6.4
#:sdk Aspire.AppHost.Sdk@13.6.4
#:property AspireUseCliBundle=true
#:project ../Logistics.Backend/Logistics.Backend.csproj

using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.Logistics_Backend>("Backend")
    .WithHttpHealthCheck("/health");

if (!builder.Environment.IsDevelopment())
{
    var postgres = builder.AddPostgres("postgres");
    var postgresdb = postgres.AddDatabase("LogisticsDatabase");

    backend.WithReference(postgresdb)
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
        .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
        .WaitFor(postgresdb);
}

builder.AddJavaScriptApp("Frontend", "../..", runScriptName: "start")
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("BACKEND_PORT", backend.GetEndpoint("http").Property(EndpointProperty.Port))
    .WithReference(backend)
    .WaitFor(backend);

builder.Build().Run();
