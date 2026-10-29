using Asp.Versioning.Builder;
using Logistics.Backend.Extensions;
using Logistics.Backend.Infrastructure;
using Logistics.Backend.Model.Vehicles;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    private static async Task<Results<Ok<VehicleListResponse>, BadRequest>> ListVehicles(
        LogisticsContext dbContext,
        [AsParameters] QueryVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Vehicle> query = dbContext.Vehicles.AsNoTracking().ApplyFilters(request);

        int totalCount = await query.CountAsync(cancellationToken);

        // Apply ordering
        // Implement ordering logic based on the orderBy parameter
        // For simplicity, let's assume we only support ordering by "createdAt" or "updatedAt"
        if (string.IsNullOrEmpty(request.OrderBy) ||
            request.OrderBy.Equals("createdAt", StringComparison.OrdinalIgnoreCase))
        {
            query = query.OrderBy(v => v.CreatedAt).ThenBy(v => v.Id);
        }
        else if (request.OrderBy.Equals("updatedAt", StringComparison.OrdinalIgnoreCase))
        {
            query = query.OrderBy(v => v.UpdatedAt).ThenBy(v => v.Id);
        }
        else
        {
            return TypedResults.BadRequest();
        }

        // Implement pagination logic based on pageSize and pageToken
        var vehicles = await query.Take(request.PageSize ?? 25).ToListAsync(cancellationToken);

        var response = new VehicleListResponse
        {
            Items = vehicles,
            TotalCount = totalCount,
            NextPageToken = null // Implement logic to generate next page token if needed
        };

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<Vehicle>, NotFound>> GetVehicleById(
        Guid vehicleId,
        LogisticsContext dbContext,
        CancellationToken cancellationToken = default)
    {
        Vehicle? vehicle = await dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        return vehicle is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(vehicle);
    }

    private static async Task<Results<Created<Vehicle>, BadRequest, Conflict>> CreateVehicle(
        CreateVehicleRequest createVehicle,
        LogisticsContext dbContext,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        bool exists = await dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(
                v => v.Vin == createVehicle.Vin || v.LicensePlate == createVehicle.LicensePlate,
                cancellationToken);

        if (exists)
        {
            return TypedResults.Conflict();
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

        return TypedResults.Created($"/v1/vehicles/{vehicle.Id}", vehicle);
    }

    private static async Task<Results<Ok<Vehicle>, BadRequest, NotFound, Conflict>> ReplaceVehicle(
        Guid vehicleId,
        UpdateVehicleRequest updateVehicle,
        LogisticsContext dbContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        Vehicle? vehicle = await dbContext.Vehicles.FindAsync([vehicleId], cancellationToken);
        if (vehicle is null)
        {
            return TypedResults.NotFound();
        }

        bool hasConflict = await dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(
                v => v.Id != vehicleId &&
                     (v.Vin == updateVehicle.Vin || v.LicensePlate == updateVehicle.LicensePlate),
                cancellationToken);

        if (hasConflict)
        {
            return TypedResults.Conflict();
        }

        dbContext.Entry(vehicle).CurrentValues.SetValues(updateVehicle);
        vehicle.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(vehicle);
    }

    private static async Task<Results<Ok<Vehicle>, BadRequest, NotFound, Conflict>> PatchVehicle(
        Guid vehicleId,
        PatchVehicleRequest patchVehicle,
        LogisticsContext dbContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        Vehicle? vehicle = await dbContext.Vehicles.FindAsync([vehicleId], cancellationToken);
        if (vehicle is null)
        {
            return TypedResults.NotFound();
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
                return TypedResults.Conflict();
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

        return TypedResults.Ok(vehicle);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteVehicle(
        Guid vehicleId,
        LogisticsContext dbContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        int deletedCount = await dbContext.Vehicles
            .Where(v => v.Id == vehicleId)
            .ExecuteDeleteAsync(cancellationToken);

        return deletedCount == 0
            ? TypedResults.NotFound()
            : TypedResults.NoContent();
    }
}