using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Logistics.Backend.Model.Routes;

using RouteEntity = Logistics.Backend.Model.Routes.Route;

namespace Logistics.Backend.Extensions;

public readonly record struct RouteCursor(DateTimeOffset Timestamp, Guid LastId);

public static class RouteExtensions
{
    public static IQueryable<RouteEntity> ApplyFilters(this IQueryable<RouteEntity> query, QueryRouteRequest request)
    {
        if (request.Status.HasValue)
        {
            query = query.Where(r => r.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string search = request.Search.Trim().ToLower();
            query = query.Where(r =>
                r.Code.ToLower().Contains(search) ||
                r.Name.ToLower().Contains(search) ||
                (r.Description != null && r.Description.ToLower().Contains(search)));
        }

        return query;
    }

    public static bool TryApplyCursorAndOrdering(
        this IQueryable<RouteEntity> query,
        string? orderBy,
        string? pageToken,
        out IQueryable<RouteEntity> resultQuery,
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
            if (!TryDecodeCursor(pageToken, out RouteCursor cursor))
            {
                return false;
            }

            DateTimeOffset cursorTime = cursor.Timestamp;
            Guid cursorId = cursor.LastId;

            if (sortField == "createdAt")
            {
                resultQuery = descending
                    ? resultQuery.Where(r =>
                        r.CreatedAt < cursorTime ||
                        (r.CreatedAt == cursorTime && r.Id.CompareTo(cursorId) < 0))
                    : resultQuery.Where(r =>
                        r.CreatedAt > cursorTime ||
                        (r.CreatedAt == cursorTime && r.Id.CompareTo(cursorId) > 0));
            }
            else
            {
                resultQuery = descending
                    ? resultQuery.Where(r =>
                        r.UpdatedAt < cursorTime ||
                        (r.UpdatedAt == cursorTime && r.Id.CompareTo(cursorId) < 0))
                    : resultQuery.Where(r =>
                        r.UpdatedAt > cursorTime ||
                        (r.UpdatedAt == cursorTime && r.Id.CompareTo(cursorId) > 0));
            }
        }

        if (sortField == "createdAt")
        {
            resultQuery = descending
                ? resultQuery.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                : resultQuery.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id);
        }
        else
        {
            resultQuery = descending
                ? resultQuery.OrderByDescending(r => r.UpdatedAt).ThenByDescending(r => r.Id)
                : resultQuery.OrderBy(r => r.UpdatedAt).ThenBy(r => r.Id);
        }

        return true;
    }

    public static string EncodeCursor(this RouteEntity route, string sortField)
    {
        DateTimeOffset timestamp = sortField == "updatedAt" ? route.UpdatedAt : route.CreatedAt;
        RouteCursor cursor = new(timestamp, route.Id);
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(cursor);
        return Convert.ToBase64String(jsonBytes);
    }

    private static bool TryDecodeCursor(string pageToken, out RouteCursor cursor)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(pageToken);
            cursor = JsonSerializer.Deserialize<RouteCursor>(bytes);
            return cursor.LastId != Guid.Empty;
        }
        catch
        {
            cursor = default;
            return false;
        }
    }

    public static string GetETag(this RouteEntity route)
    {
        long normalizedTicks = route.UpdatedAt.UtcTicks / 1000;
        byte[] bytes = Encoding.UTF8.GetBytes($"{route.Id:N}:{normalizedTicks}");
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..16];
        return $"\"v1-{hash}\"";
    }

    public static bool MatchesETag(this RouteEntity route, string? ifMatch)
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

        string currentETag = route.GetETag();
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
