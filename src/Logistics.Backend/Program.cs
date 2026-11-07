using Asp.Versioning;
using Logistics.Backend.Apis;
using Logistics.Backend.Extensions;
using Logistics.Backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();

var withApiVersioning = builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;

    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

withApiVersioning.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

withApiVersioning.AddLogisticsOpenApi();

if (builder.Environment.IsDevelopment())
{
    builder.AddSqliteDbContext<LogisticsContext>("LogisticsDatabase");
}
else
{
    builder.AddNpgsqlDbContext<LogisticsContext>(
        connectionName: "LogisticsDatabase",
        configureSettings: settings =>
        {
            var csBuilder = new NpgsqlConnectionStringBuilder(settings.ConnectionString)
            {
                MaxPoolSize = 80,
                MinPoolSize = 10,
                Timeout = 30,
                MaxAutoPrepare = 32,
                AutoPrepareMinUsages = 2
            };
            settings.ConnectionString = csBuilder.ConnectionString;
        },
        configureDbContextOptions: options =>
        {
            options.UseNpgsql(npgsqlOptions =>
            {
                npgsqlOptions.MaxBatchSize(64);
            });
        });
}
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

using (IServiceScope scope = app.Services.CreateScope())
{
    LogisticsContext db = scope.ServiceProvider.GetRequiredService<LogisticsContext>();
    db.Database.EnsureCreated();
}

app.MapDefaultEndpoints();
app.MapOpenApi().WithDocumentPerVersion();
app.MapOpenApi("/openapi/{documentName}.yaml").WithDocumentPerVersion();

app.MapVehicleApi();
app.MapDriverApi();
app.MapRouteApi();

app.Run();

public partial class Program;

