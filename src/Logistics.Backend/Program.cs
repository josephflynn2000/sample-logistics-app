using Asp.Versioning;
using Logistics.Backend.Apis;
using Logistics.Backend.Extensions;
using Logistics.Backend.Infrastructure;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddDbContext<LogisticsContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("LogisticsDatabase"));
});
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

using (IServiceScope scope = app.Services.CreateScope())
{
    LogisticsContext db = scope.ServiceProvider.GetRequiredService<LogisticsContext>();
    db.Database.EnsureCreated();
}

app.MapOpenApi().WithDocumentPerVersion();
app.MapOpenApi("/openapi/{documentName}.yaml").WithDocumentPerVersion();

app.MapVehicleApi();
app.MapDriverApi();
app.MapRouteApi();

app.Run();
