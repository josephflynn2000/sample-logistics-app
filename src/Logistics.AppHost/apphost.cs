#:package Aspire.Hosting.JavaScript@13.6.4
#:sdk Aspire.AppHost.Sdk@13.6.4
#:property AspireUseCliBundle=true
#:project ../Logistics.Backend/Logistics.Backend.csproj

var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.Logistics_Backend>("Backend");

builder.AddJavaScriptApp("Frontend", "../..", runScriptName: "start")
    .WithHttpEndpoint(env: "PORT")
    .WithReference(backend)
    .WaitFor(backend);

builder.Build().Run();
