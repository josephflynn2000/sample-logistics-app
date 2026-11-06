using Asp.Versioning.Builder;
using Logistics.Backend.Extensions;
using Logistics.Backend.Infrastructure;
using Logistics.Backend.Model.Vehicles;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Logistics.Backend.Apis;

public static class VehicleApi
{
    public static void MapVehicleApi(this IEndpointRouteBuilder app)
    {
        IVersionedEndpointRouteBuilder versionSet = app
            .NewVersionedApi("Vehicle API");
        RouteGroupBuilder group = versionSet
            .MapGroup("/v{version:apiVersion}/vehicles")
            .HasApiVersion(1, 0)
            .WithTags("Vehicles");

        group.MapPost("/", CreateVehicle)
            .WithName("Vehicles_Create")
            .WithSummary("Register a new vehicle")
            .WithDescription("Creates a new vehicle record in the fleet registry. Returns `201 Created` with a `Location` header pointing to the new resource URI and an `ETag` header for optimistic concurrency control.")
            .Accepts<CreateVehicleRequest>("application/json")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/", ListVehicles)
            .WithName("Vehicles_List")
            .WithSummary("List vehicles")
            .WithDescription("Retrieves a paginated list of vehicles, with optional filtering by operational status, vehicle type, minimum payload capacity, or free-text search.")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/{vehicleId}", GetVehicleById)
            .WithName("Vehicles_Get")
            .WithSummary("Get a vehicle by ID")
            .WithDescription("Retrieves the full details of a specific vehicle by its unique identifier.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPut("/{vehicleId}", ReplaceVehicle)
            .WithName("Vehicles_Replace")
            .WithSummary("Replace a vehicle")
            .WithDescription("Performs a full replacement of an existing vehicle record. Clients may supply an `If-Match` header with the resource's current `ETag` to prevent lost updates.")
            .Accepts<UpdateVehicleRequest>("application/json")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPatch("/{vehicleId}", PatchVehicle)
            .WithName("Vehicles_Update")
            .WithSummary("Partially update a vehicle")
            .WithDescription("Applies a partial update to an existing vehicle using JSON Merge Patch (RFC 7396) semantics. Only provided fields are modified.")
            .Accepts<PatchVehicleRequest>("application/merge-patch+json", "application/json")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapDelete("/{vehicleId}", DeleteVehicle)
            .WithName("Vehicles_Delete")
            .WithSummary("Delete a vehicle")
            .WithDescription("Permanently removes a vehicle record from the registry.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<VehicleListResponse>, ProblemHttpResult>> ListVehicles(
        LogisticsContext dbContext,
        [AsParameters] QueryVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Vehicle> filteredQuery = dbContext.Vehicles.AsNoTracking().ApplyFilters(request);

        if (!filteredQuery.TryApplyCursorAndOrdering(
                request.OrderBy,
                request.PageToken,
                out IQueryable<Vehicle> pagedQuery,
                out string sortField))
        {
            return TypedResults.Problem(
                detail: "The specified orderBy or pageToken query parameter is invalid.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        int pageSize = request.PageSize ?? 25;
        List<Vehicle> vehicles = await pagedQuery
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        int totalCount;
        if (string.IsNullOrWhiteSpace(request.PageToken) && vehicles.Count <= pageSize)
        {
            totalCount = vehicles.Count;
        }
        else
        {
            totalCount = await filteredQuery.CountAsync(cancellationToken);
        }

        string? nextPageToken = null;
        if (vehicles.Count > pageSize)
        {
            vehicles.RemoveAt(pageSize);
            nextPageToken = vehicles[^1].EncodeCursor(sortField);
        }

        var response = new VehicleListResponse
        {
            Items = vehicles,
            TotalCount = totalCount,
            NextPageToken = nextPageToken
        };

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<Vehicle>, ProblemHttpResult>> GetVehicleById(
        Guid vehicleId,
        LogisticsContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        Vehicle? vehicle = await dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle is null)
        {
            return TypedResults.Problem(
                detail: "The requested vehicle was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        httpContext.Response.Headers.ETag = vehicle.GetETag();
        return TypedResults.Ok(vehicle);
    }

    private static async Task<Results<Created<Vehicle>, ProblemHttpResult>> CreateVehicle(
        CreateVehicleRequest createVehicle,
        LogisticsContext dbContext,
        IMemoryCache memoryCache,
        HttpContext httpContext,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        string? cacheKey = idempotencyKey.HasValue
            ? $"vehicles:idempotency:{idempotencyKey.Value:N}"
            : null;

        if (cacheKey is not null &&
            memoryCache.TryGetValue(cacheKey, out Vehicle? cachedVehicle) &&
            cachedVehicle is not null)
        {
            httpContext.Response.Headers.ETag = cachedVehicle.GetETag();
            return TypedResults.Created($"/v1/vehicles/{cachedVehicle.Id}", cachedVehicle);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Vehicle vehicle = new()
        {
            Id = Guid.NewGuid(),
            Vin = createVehicle.Vin,
            LicensePlate = createVehicle.LicensePlate,
            Make = createVehicle.Make,
            Model = createVehicle.Model,
            Year = createVehicle.Year,
            Type = createVehicle.Type,
            CapacityKg = createVehicle.CapacityKg,
            CapacityVolumeM3 = createVehicle.CapacityVolumeM3,
            Status = createVehicle.Status ?? VehicleStatus.Available,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Vehicles.Add(vehicle);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return TypedResults.Problem(
                detail: "A vehicle with the same VIN or license plate already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (cacheKey is not null)
        {
            memoryCache.Set(cacheKey, vehicle, TimeSpan.FromHours(24));
        }

        httpContext.Response.Headers.ETag = vehicle.GetETag();
        return TypedResults.Created($"/v1/vehicles/{vehicle.Id}", vehicle);
    }

    private static async Task<Results<Ok<Vehicle>, ProblemHttpResult>> ReplaceVehicle(
        Guid vehicleId,
        UpdateVehicleRequest updateVehicle,
        LogisticsContext dbContext,
        HttpContext httpContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        Vehicle? vehicle = await dbContext.Vehicles.FindAsync([vehicleId], cancellationToken);
        if (vehicle is null)
        {
            return TypedResults.Problem(
                detail: "The requested vehicle was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!vehicle.MatchesETag(ifMatch))
        {
            return TypedResults.Problem(
                detail: "The resource ETag does not match the provided If-Match header value.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        bool isUnconditional = string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*";

        for (int attempt = 0; ; attempt++)
        {
            dbContext.Entry(vehicle).CurrentValues.SetValues(updateVehicle);
            vehicle.UpdatedAt = vehicle.NextUpdatedAt();

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (isUnconditional && attempt < 2)
            {
                await dbContext.Entry(vehicle).ReloadAsync(cancellationToken);
                if (dbContext.Entry(vehicle).State == EntityState.Detached)
                {
                    return TypedResults.Problem(
                        detail: "The requested vehicle was not found.",
                        statusCode: StatusCodes.Status404NotFound);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return TypedResults.Problem(
                    detail: "The resource was modified or deleted by another concurrent request.",
                    statusCode: StatusCodes.Status412PreconditionFailed);
            }
            catch (DbUpdateException)
            {
                return TypedResults.Problem(
                    detail: "A vehicle with the same VIN or license plate already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        httpContext.Response.Headers.ETag = vehicle.GetETag();
        return TypedResults.Ok(vehicle);
    }

    private static async Task<Results<Ok<Vehicle>, ProblemHttpResult>> PatchVehicle(
        Guid vehicleId,
        PatchVehicleRequest patchVehicle,
        LogisticsContext dbContext,
        HttpContext httpContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        Vehicle? vehicle = await dbContext.Vehicles.FindAsync([vehicleId], cancellationToken);
        if (vehicle is null)
        {
            return TypedResults.Problem(
                detail: "The requested vehicle was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!vehicle.MatchesETag(ifMatch))
        {
            return TypedResults.Problem(
                detail: "The resource ETag does not match the provided If-Match header value.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        bool isUnconditional = string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*";

        for (int attempt = 0; ; attempt++)
        {
            if (patchVehicle.LicensePlate is not null) vehicle.LicensePlate = patchVehicle.LicensePlate;
            if (patchVehicle.Make is not null) vehicle.Make = patchVehicle.Make;
            if (patchVehicle.Model is not null) vehicle.Model = patchVehicle.Model;
            if (patchVehicle.Year.HasValue) vehicle.Year = patchVehicle.Year.Value;
            if (patchVehicle.Type.HasValue) vehicle.Type = patchVehicle.Type.Value;
            if (patchVehicle.CapacityKg.HasValue) vehicle.CapacityKg = patchVehicle.CapacityKg.Value;
            if (patchVehicle.CapacityVolumeM3.HasValue) vehicle.CapacityVolumeM3 = patchVehicle.CapacityVolumeM3.Value;
            if (patchVehicle.Status.HasValue) vehicle.Status = patchVehicle.Status.Value;

            vehicle.UpdatedAt = vehicle.NextUpdatedAt();

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (isUnconditional && attempt < 2)
            {
                await dbContext.Entry(vehicle).ReloadAsync(cancellationToken);
                if (dbContext.Entry(vehicle).State == EntityState.Detached)
                {
                    return TypedResults.Problem(
                        detail: "The requested vehicle was not found.",
                        statusCode: StatusCodes.Status404NotFound);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return TypedResults.Problem(
                    detail: "The resource was modified or deleted by another concurrent request.",
                    statusCode: StatusCodes.Status412PreconditionFailed);
            }
            catch (DbUpdateException)
            {
                return TypedResults.Problem(
                    detail: "A vehicle with the same license plate already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        httpContext.Response.Headers.ETag = vehicle.GetETag();
        return TypedResults.Ok(vehicle);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteVehicle(
        Guid vehicleId,
        LogisticsContext dbContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*")
        {
            int deleted = await dbContext.Vehicles
                .Where(v => v.Id == vehicleId)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted == 0)
            {
                return TypedResults.Problem(
                    detail: "The requested vehicle was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return TypedResults.NoContent();
        }

        Vehicle? vehicle = await dbContext.Vehicles.FindAsync([vehicleId], cancellationToken);
        if (vehicle is null)
        {
            return TypedResults.Problem(
                detail: "The requested vehicle was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!vehicle.MatchesETag(ifMatch))
        {
            return TypedResults.Problem(
                detail: "The resource ETag does not match the provided If-Match header value.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        dbContext.Vehicles.Remove(vehicle);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                detail: "The resource was modified or deleted by another concurrent request.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        return TypedResults.NoContent();
    }
}
