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
            .Accepts<CreateVehicleRequest>("application/json");
        group.MapGet("/", ListVehicles);
        group.MapGet("/{vehicleId}", GetVehicleById);
        group.MapPut("/{vehicleId}", ReplaceVehicle)
            .Accepts<UpdateVehicleRequest>("application/json");
        group.MapPatch("/{vehicleId}", PatchVehicle)
            .Accepts<PatchVehicleRequest>("application/merge-patch+json", "application/json");
        group.MapDelete("/{vehicleId}", DeleteVehicle);
    }

    private static async Task<Results<Ok<VehicleListResponse>, ProblemHttpResult>> ListVehicles(
        LogisticsContext dbContext,
        [AsParameters] QueryVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Vehicle> filteredQuery = dbContext.Vehicles.AsNoTracking().ApplyFilters(request);

        int totalCount = await filteredQuery.CountAsync(cancellationToken);

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

        bool exists = await dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(
                v => v.Vin == createVehicle.Vin || v.LicensePlate == createVehicle.LicensePlate,
                cancellationToken);

        if (exists)
        {
            return TypedResults.Problem(
                detail: "A vehicle with the same VIN or license plate already exists.",
                statusCode: StatusCodes.Status409Conflict);
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
        await dbContext.SaveChangesAsync(cancellationToken);

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

        bool hasConflict = await dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(
                v => v.Id != vehicleId &&
                     (v.Vin == updateVehicle.Vin || v.LicensePlate == updateVehicle.LicensePlate),
                cancellationToken);

        if (hasConflict)
        {
            return TypedResults.Problem(
                detail: "A vehicle with the same VIN or license plate already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        dbContext.Entry(vehicle).CurrentValues.SetValues(updateVehicle);
        vehicle.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

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

        if (patchVehicle.LicensePlate is not null &&
            patchVehicle.LicensePlate != vehicle.LicensePlate)
        {
            bool plateConflict = await dbContext.Vehicles
                .AsNoTracking()
                .AnyAsync(
                    v => v.Id != vehicleId && v.LicensePlate == patchVehicle.LicensePlate,
                    cancellationToken);

            if (plateConflict)
            {
                return TypedResults.Problem(
                    detail: "A vehicle with the same license plate already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            vehicle.LicensePlate = patchVehicle.LicensePlate;
        }

        if (patchVehicle.Make is not null) vehicle.Make = patchVehicle.Make;
        if (patchVehicle.Model is not null) vehicle.Model = patchVehicle.Model;
        if (patchVehicle.Year.HasValue) vehicle.Year = patchVehicle.Year.Value;
        if (patchVehicle.Type.HasValue) vehicle.Type = patchVehicle.Type.Value;
        if (patchVehicle.CapacityKg.HasValue) vehicle.CapacityKg = patchVehicle.CapacityKg.Value;
        if (patchVehicle.CapacityVolumeM3.HasValue) vehicle.CapacityVolumeM3 = patchVehicle.CapacityVolumeM3.Value;
        if (patchVehicle.Status.HasValue) vehicle.Status = patchVehicle.Status.Value;

        vehicle.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = vehicle.GetETag();
        return TypedResults.Ok(vehicle);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteVehicle(
        Guid vehicleId,
        LogisticsContext dbContext,
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

        dbContext.Vehicles.Remove(vehicle);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
