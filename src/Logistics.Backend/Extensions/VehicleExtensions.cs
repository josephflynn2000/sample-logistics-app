using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Logistics.Backend.Model.Vehicles;

namespace Logistics.Backend.Extensions;

public readonly record struct VehicleCursor(DateTimeOffset Timestamp, Guid LastId);

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

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string search = request.Search.Trim().ToLower();
            query = query.Where(v =>
                v.Vin.ToLower().Contains(search) ||
                v.LicensePlate.ToLower().Contains(search) ||
                v.Make.ToLower().Contains(search) ||
                v.Model.ToLower().Contains(search));
        }

        return query;
    }

    public static bool TryApplyCursorAndOrdering(
        this IQueryable<Vehicle> query,
        string? orderBy,
        string? pageToken,
        out IQueryable<Vehicle> resultQuery,
        out string sortField)
    {
        resultQuery = query;
        sortField = "createdAt";
        bool descending = false;

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            string[] parts = orderBy.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 1 or > 2)
            {
                return false;
            }

            if (parts[0].Equals("createdAt", StringComparison.OrdinalIgnoreCase))
            {
                sortField = "createdAt";
            }
            else if (parts[0].Equals("updatedAt", StringComparison.OrdinalIgnoreCase))
            {
                sortField = "updatedAt";
            }
            else
            {
                return false;
            }

            if (parts.Length == 2)
            {
                if (parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase))
                {
                    descending = true;
                }
                else if (!parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            if (!TryDecodeCursor(pageToken, out VehicleCursor cursor))
            {
                return false;
            }

            DateTimeOffset cursorTime = cursor.Timestamp;
            Guid cursorId = cursor.LastId;

            if (sortField == "createdAt")
            {
                resultQuery = descending
                    ? resultQuery.Where(v =>
                        v.CreatedAt < cursorTime ||
                        (v.CreatedAt == cursorTime && v.Id.CompareTo(cursorId) < 0))
                    : resultQuery.Where(v =>
                        v.CreatedAt > cursorTime ||
                        (v.CreatedAt == cursorTime && v.Id.CompareTo(cursorId) > 0));
            }
            else
            {
                resultQuery = descending
                    ? resultQuery.Where(v =>
                        v.UpdatedAt < cursorTime ||
                        (v.UpdatedAt == cursorTime && v.Id.CompareTo(cursorId) < 0))
                    : resultQuery.Where(v =>
                        v.UpdatedAt > cursorTime ||
                        (v.UpdatedAt == cursorTime && v.Id.CompareTo(cursorId) > 0));
            }
        }

        if (sortField == "createdAt")
        {
            resultQuery = descending
                ? resultQuery.OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                : resultQuery.OrderBy(v => v.CreatedAt).ThenBy(v => v.Id);
        }
        else
        {
            resultQuery = descending
                ? resultQuery.OrderByDescending(v => v.UpdatedAt).ThenByDescending(v => v.Id)
                : resultQuery.OrderBy(v => v.UpdatedAt).ThenBy(v => v.Id);
        }

        return true;
    }

    public static string EncodeCursor(this Vehicle vehicle, string sortField)
    {
        DateTimeOffset timestamp = sortField == "updatedAt" ? vehicle.UpdatedAt : vehicle.CreatedAt;
        VehicleCursor cursor = new(timestamp, vehicle.Id);
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(cursor);
        return Convert.ToBase64String(jsonBytes);
    }

    private static bool TryDecodeCursor(string pageToken, out VehicleCursor cursor)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(pageToken);
            cursor = JsonSerializer.Deserialize<VehicleCursor>(bytes);
            return cursor.LastId != Guid.Empty;
        }
        catch
        {
            cursor = default;
            return false;
        }
    }

    public static string GetETag(this Vehicle vehicle)
    {
        long normalizedTicks = vehicle.UpdatedAt.UtcTicks / 1000;
        byte[] bytes = Encoding.UTF8.GetBytes($"{vehicle.Id:N}:{normalizedTicks}");
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..16];
        return $"\"v1-{hash}\"";
    }

    public static bool MatchesETag(this Vehicle vehicle, string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return true;
        }

        string trimmed = ifMatch.Trim();
        if (trimmed == "*")
        {
            return true;
        }

        string currentETag = vehicle.GetETag();
        foreach (string candidate in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(candidate, currentETag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
