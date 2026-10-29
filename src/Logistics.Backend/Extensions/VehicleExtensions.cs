using Logistics.Backend.Model.Vehicles;

namespace Logistics.Backend.Extensions;

public static class VehicleExtensions
{
    public static IQueryable<Vehicle> ApplyFilters(this IQueryable<Vehicle> query, QueryVehicleRequest request)
    {
        if (request.Status.HasValue)
        {
            query = query.Where(v => v.Status == request.Status.Value);
        }

        if (request.Type.HasValue)
        {
            query = query.Where(v => v.Type == request.Type.Value);
        }

        if (request.MinCapacityKg.HasValue)
        {
            query = query.Where(v => v.CapacityKg >= request.MinCapacityKg.Value);
        }

        if (!string.IsNullOrEmpty(request.Search))
        {
            query = query.Where(v => v.LicensePlate.Contains(request.Search) || v.Make.Contains(request.Search) || v.Model.Contains(request.Search));
        }

        return query;
    }


}