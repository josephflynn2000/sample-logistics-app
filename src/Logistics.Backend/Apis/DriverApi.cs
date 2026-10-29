using Asp.Versioning.Builder;
using Logistics.Backend.Extensions;
using Logistics.Backend.Infrastructure;
using Logistics.Backend.Model.Drivers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Logistics.Backend.Apis;

public static class DriverApi
{
    public static void MapDriverApi(this IEndpointRouteBuilder app)
    {
        IVersionedEndpointRouteBuilder versionSet = app
            .NewVersionedApi("Driver API");
        RouteGroupBuilder group = versionSet
            .MapGroup("/v{version:apiVersion}/drivers")
            .HasApiVersion(1, 0)
            .WithTags("Drivers");

        group.MapPost("/", CreateDriver)
            .WithName("Drivers_Create")
            .WithSummary("Register a new driver")
            .WithDescription("Creates a new driver record. Returns `201 Created` with `Location` and `ETag` headers.")
            .Accepts<CreateDriverRequest>("application/json")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/", ListDrivers)
            .WithName("Drivers_List")
            .WithSummary("List drivers")
            .WithDescription("Retrieves a paginated list of drivers, with optional filtering by status, commercial license class, or search query.")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/{driverId}", GetDriverById)
            .WithName("Drivers_Get")
            .WithSummary("Get a driver by ID")
            .WithDescription("Retrieves the full details of a specific driver by their unique identifier.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPut("/{driverId}", ReplaceDriver)
            .WithName("Drivers_Replace")
            .WithSummary("Replace a driver")
            .WithDescription("Performs a full replacement of an existing driver record.")
            .Accepts<UpdateDriverRequest>("application/json")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPatch("/{driverId}", PatchDriver)
            .WithName("Drivers_Update")
            .WithSummary("Partially update a driver")
            .WithDescription("Applies a partial update to an existing driver record using JSON Merge Patch (RFC 7396) semantics.")
            .Accepts<PatchDriverRequest>("application/merge-patch+json", "application/json")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapDelete("/{driverId}", DeleteDriver)
            .WithName("Drivers_Delete")
            .WithSummary("Delete a driver")
            .WithDescription("Permanently removes a driver record from the registry.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<DriverListResponse>, ProblemHttpResult>> ListDrivers(
        LogisticsContext dbContext,
        [AsParameters] QueryDriverRequest request,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Driver> filteredQuery = dbContext.Drivers.AsNoTracking().ApplyFilters(request);

        int totalCount = await filteredQuery.CountAsync(cancellationToken);

        if (!filteredQuery.TryApplyCursorAndOrdering(
                request.OrderBy,
                request.PageToken,
                out IQueryable<Driver> pagedQuery,
                out string sortField))
        {
            return TypedResults.Problem(
                detail: "The specified orderBy or pageToken query parameter is invalid.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        int pageSize = request.PageSize ?? 25;
        List<Driver> drivers = await pagedQuery
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        string? nextPageToken = null;
        if (drivers.Count > pageSize)
        {
            drivers.RemoveAt(pageSize);
            nextPageToken = drivers[^1].EncodeCursor(sortField);
        }

        var response = new DriverListResponse
        {
            Items = drivers,
            TotalCount = totalCount,
            NextPageToken = nextPageToken
        };

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<Driver>, ProblemHttpResult>> GetDriverById(
        Guid driverId,
        LogisticsContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        Driver? driver = await dbContext.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        if (driver is null)
        {
            return TypedResults.Problem(
                detail: "The requested driver was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        httpContext.Response.Headers.ETag = driver.GetETag();
        return TypedResults.Ok(driver);
    }

    private static async Task<Results<Created<Driver>, ProblemHttpResult>> CreateDriver(
        CreateDriverRequest createDriver,
        LogisticsContext dbContext,
        IMemoryCache memoryCache,
        HttpContext httpContext,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        string? cacheKey = idempotencyKey.HasValue
            ? $"drivers:idempotency:{idempotencyKey.Value:N}"
            : null;

        if (cacheKey is not null &&
            memoryCache.TryGetValue(cacheKey, out Driver? cachedDriver) &&
            cachedDriver is not null)
        {
            httpContext.Response.Headers.ETag = cachedDriver.GetETag();
            return TypedResults.Created($"/v1/drivers/{cachedDriver.Id}", cachedDriver);
        }

        bool exists = await dbContext.Drivers
            .AsNoTracking()
            .AnyAsync(
                d => d.EmployeeNumber == createDriver.EmployeeNumber ||
                     d.Email == createDriver.Email ||
                     d.LicenseNumber == createDriver.LicenseNumber,
                cancellationToken);

        if (exists)
        {
            return TypedResults.Problem(
                detail: "A driver with the same employee number, email, or license number already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Driver driver = new()
        {
            Id = Guid.NewGuid(),
            EmployeeNumber = createDriver.EmployeeNumber,
            FirstName = createDriver.FirstName,
            LastName = createDriver.LastName,
            Email = createDriver.Email,
            Phone = createDriver.Phone,
            LicenseNumber = createDriver.LicenseNumber,
            LicenseClass = createDriver.LicenseClass,
            LicenseExpiryDate = createDriver.LicenseExpiryDate,
            Status = createDriver.Status ?? DriverStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (cacheKey is not null)
        {
            memoryCache.Set(cacheKey, driver, TimeSpan.FromHours(24));
        }

        httpContext.Response.Headers.ETag = driver.GetETag();
        return TypedResults.Created($"/v1/drivers/{driver.Id}", driver);
    }

    private static async Task<Results<Ok<Driver>, ProblemHttpResult>> ReplaceDriver(
        Guid driverId,
        UpdateDriverRequest updateDriver,
        LogisticsContext dbContext,
        HttpContext httpContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        Driver? driver = await dbContext.Drivers.FindAsync([driverId], cancellationToken);
        if (driver is null)
        {
            return TypedResults.Problem(
                detail: "The requested driver was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!driver.MatchesETag(ifMatch))
        {
            return TypedResults.Problem(
                detail: "The resource ETag does not match the provided If-Match header value.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        bool hasConflict = await dbContext.Drivers
            .AsNoTracking()
            .AnyAsync(
                d => d.Id != driverId &&
                     (d.EmployeeNumber == updateDriver.EmployeeNumber ||
                      d.Email == updateDriver.Email ||
                      d.LicenseNumber == updateDriver.LicenseNumber),
                cancellationToken);

        if (hasConflict)
        {
            return TypedResults.Problem(
                detail: "A driver with the same employee number, email, or license number already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        dbContext.Entry(driver).CurrentValues.SetValues(updateDriver);
        driver.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = driver.GetETag();
        return TypedResults.Ok(driver);
    }

    private static async Task<Results<Ok<Driver>, ProblemHttpResult>> PatchDriver(
        Guid driverId,
        PatchDriverRequest patchDriver,
        LogisticsContext dbContext,
        HttpContext httpContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        Driver? driver = await dbContext.Drivers.FindAsync([driverId], cancellationToken);
        if (driver is null)
        {
            return TypedResults.Problem(
                detail: "The requested driver was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!driver.MatchesETag(ifMatch))
        {
            return TypedResults.Problem(
                detail: "The resource ETag does not match the provided If-Match header value.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        if (patchDriver.Email is not null && patchDriver.Email != driver.Email)
        {
            bool emailConflict = await dbContext.Drivers
                .AsNoTracking()
                .AnyAsync(
                    d => d.Id != driverId && d.Email == patchDriver.Email,
                    cancellationToken);

            if (emailConflict)
            {
                return TypedResults.Problem(
                    detail: "A driver with the same email already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            driver.Email = patchDriver.Email;
        }

        if (patchDriver.LicenseNumber is not null && patchDriver.LicenseNumber != driver.LicenseNumber)
        {
            bool licenseConflict = await dbContext.Drivers
                .AsNoTracking()
                .AnyAsync(
                    d => d.Id != driverId && d.LicenseNumber == patchDriver.LicenseNumber,
                    cancellationToken);

            if (licenseConflict)
            {
                return TypedResults.Problem(
                    detail: "A driver with the same license number already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            driver.LicenseNumber = patchDriver.LicenseNumber;
        }

        if (patchDriver.FirstName is not null) driver.FirstName = patchDriver.FirstName;
        if (patchDriver.LastName is not null) driver.LastName = patchDriver.LastName;
        if (patchDriver.Phone is not null) driver.Phone = patchDriver.Phone;
        if (patchDriver.LicenseClass.HasValue) driver.LicenseClass = patchDriver.LicenseClass.Value;
        if (patchDriver.LicenseExpiryDate is not null) driver.LicenseExpiryDate = patchDriver.LicenseExpiryDate;
        if (patchDriver.Status.HasValue) driver.Status = patchDriver.Status.Value;

        driver.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = driver.GetETag();
        return TypedResults.Ok(driver);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteDriver(
        Guid driverId,
        LogisticsContext dbContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        Driver? driver = await dbContext.Drivers.FindAsync([driverId], cancellationToken);
        if (driver is null)
        {
            return TypedResults.Problem(
                detail: "The requested driver was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!driver.MatchesETag(ifMatch))
        {
            return TypedResults.Problem(
                detail: "The resource ETag does not match the provided If-Match header value.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        dbContext.Drivers.Remove(driver);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
