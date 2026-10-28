using Asp.Versioning;
using Asp.Versioning.Builder;

using Logistics.Backend.Model.Vehicles;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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

    private static Results<Ok<VehicleListResponse>, BadRequest> ListVehicles(
        [FromQuery] int? pageSize = 25,
        [FromQuery] string? pageToken = null,
        [FromQuery] string? orderBy = null,
        [FromQuery] VehicleStatus? status = null,
        [FromQuery] VehicleType? type = null,
        [FromQuery] double? minCapacityKg = null,
        [FromQuery] string? search = null)
    {
        throw new NotImplementedException("ListVehicles method is not implemented yet.");
    }

    private static Results<Ok<Vehicle>, NotFound> GetVehicleById(Guid vehicleId)
    {
        // Implementation for retrieving a vehicle by ID
        throw new NotImplementedException("GetVehicleById method is not implemented yet.");
    }

    private static Results<Created<Vehicle>, BadRequest, Conflict> CreateVehicle(
        CreateVehicleRequest createVehicle,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey = null)
    {
        throw new NotImplementedException("CreateVehicle method is not implemented yet.");
    }

    private static Results<Ok<Vehicle>, BadRequest, NotFound, Conflict, ProblemHttpResult> ReplaceVehicle(
        Guid vehicleId,
        UpdateVehicleRequest updateVehicle,
        [FromHeader(Name = "If-Match")] string? ifMatch = null)
    {
        throw new NotImplementedException("ReplaceVehicle method is not implemented yet.");
    }

    private static Results<Ok<Vehicle>, BadRequest, NotFound, Conflict, ProblemHttpResult> PatchVehicle(
        Guid vehicleId,
        PatchVehicleRequest patchVehicle,
        [FromHeader(Name = "If-Match")] string? ifMatch = null)
    {
        throw new NotImplementedException("PatchVehicle method is not implemented yet.");
    }

    private static Results<NoContent, NotFound, ProblemHttpResult> DeleteVehicle(
        Guid vehicleId,
        [FromHeader(Name = "If-Match")] string? ifMatch = null)
    {
        throw new NotImplementedException("DeleteVehicle method is not implemented yet.");
    }
}