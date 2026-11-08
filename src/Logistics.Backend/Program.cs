using Asp.Versioning;
using Logistics.Backend.Apis;
using Logistics.Backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
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

withApiVersioning.AddOpenApi(options =>
{
    options.Document.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Logistics Management REST API",
            Version = "1.0.0",
            Description = """
                RESTful HTTP API for logistics analysts to register and manage core fleet and transit resources:
                - **Vehicles**: Fleet vehicles, payload capacities, and operational statuses.
                - **Drivers**: Driver personnel records, commercial license classifications, and availability statuses.
                - **Routes**: Transit and delivery routes composed of an ordered sequence of geographic waypoints.

                ### REST Design Principles
                This API follows:
                - **Core REST Principles**: Resource-oriented plural nouns, standard HTTP method semantics (`GET`, `POST`, `PUT`, `PATCH`, `DELETE`), statelessness, and hypermedia/location headers.
                - **Microsoft Azure REST API Guidelines**: Explicit URI path versioning (`/v1`), optimistic concurrency control via `ETag` and `If-Match` headers, `Idempotency-Key` support for safe retries on `POST`, and standardized `application/problem+json` error payloads.
                - **Google Cloud API Design Guide (AIPs)**: Token-based pagination (`pageSize`, `pageToken`, `nextPageToken`, `totalCount`), standard collection filtering (`status`, `search`), and consistent resource lifecycle timestamps (`createdAt`, `updatedAt`).
                """.ReplaceLineEndings("\n"),
            Contact = new OpenApiContact
            {
                Name = "Logistics Platform Team"
            },
            License = new OpenApiLicense
            {
                Name = "MIT",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        };

        document.Tags = new HashSet<OpenApiTag>
        {
            new()
            {
                Name = "Vehicles",
                Description = "Operations for registering and managing fleet vehicles."
            },
            new()
            {
                Name = "Drivers",
                Description = "Operations for registering and managing drivers and commercial license details."
            },
            new()
            {
                Name = "Routes",
                Description = "Operations for defining and managing transit/delivery routes and their ordered waypoints."
            }
        };

        return Task.CompletedTask;
    });
});

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

if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("LogisticsDatabase")))
{
    using IServiceScope scope = app.Services.CreateScope();
    LogisticsContext db = scope.ServiceProvider.GetRequiredService<LogisticsContext>();
    db.Database.EnsureCreated();
}

app.MapDefaultEndpoints();
app.MapOpenApi().WithDocumentPerVersion();

app.MapVehicleApi();
app.MapDriverApi();
app.MapRouteApi();

app.Run();

public partial class Program;