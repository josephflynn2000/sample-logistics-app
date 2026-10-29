using Asp.Versioning;

using Logistics.Backend.Apis;
using Logistics.Backend.Infrastructure;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidation();
builder.Services.AddProblemDetails();

var withApiVersioning = builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;

    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddDbContext<LogisticsContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("LogisticsDatabase"));
});
var app = builder.Build();

app.MapVehicleApi();

app.Run();