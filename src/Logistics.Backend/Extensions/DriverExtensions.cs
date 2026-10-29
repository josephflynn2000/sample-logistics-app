using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Logistics.Backend.Model.Drivers;

namespace Logistics.Backend.Extensions;

public readonly record struct DriverCursor(DateTimeOffset Timestamp, Guid LastId);

public static class DriverExtensions
{
    public static IQueryable<Driver> ApplyFilters(this IQueryable<Driver> query, QueryDriverRequest request)
    {
        if (request.Status.HasValue)
        {
            query = query.Where(d => d.Status == request.Status.Value);
        }

        if (request.LicenseClass.HasValue)
        {
            query = query.Where(d => d.LicenseClass == request.LicenseClass.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string search = request.Search.Trim().ToLower();
            query = query.Where(d =>
                d.EmployeeNumber.ToLower().Contains(search) ||
                d.FirstName.ToLower().Contains(search) ||
                d.LastName.ToLower().Contains(search) ||
                d.Email.ToLower().Contains(search) ||
                d.LicenseNumber.ToLower().Contains(search));
        }

        return query;
    }

    public static bool TryApplyCursorAndOrdering(
        this IQueryable<Driver> query,
        string? orderBy,
        string? pageToken,
        out IQueryable<Driver> resultQuery,
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
            if (!TryDecodeCursor(pageToken, out DriverCursor cursor))
            {
                return false;
            }

            DateTimeOffset cursorTime = cursor.Timestamp;
            Guid cursorId = cursor.LastId;

            if (sortField == "createdAt")
            {
                resultQuery = descending
                    ? resultQuery.Where(d =>
                        d.CreatedAt < cursorTime ||
                        (d.CreatedAt == cursorTime && d.Id.CompareTo(cursorId) < 0))
                    : resultQuery.Where(d =>
                        d.CreatedAt > cursorTime ||
                        (d.CreatedAt == cursorTime && d.Id.CompareTo(cursorId) > 0));
            }
            else
            {
                resultQuery = descending
                    ? resultQuery.Where(d =>
                        d.UpdatedAt < cursorTime ||
                        (d.UpdatedAt == cursorTime && d.Id.CompareTo(cursorId) < 0))
                    : resultQuery.Where(d =>
                        d.UpdatedAt > cursorTime ||
                        (d.UpdatedAt == cursorTime && d.Id.CompareTo(cursorId) > 0));
            }
        }

        if (sortField == "createdAt")
        {
            resultQuery = descending
                ? resultQuery.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
                : resultQuery.OrderBy(d => d.CreatedAt).ThenBy(d => d.Id);
        }
        else
        {
            resultQuery = descending
                ? resultQuery.OrderByDescending(d => d.UpdatedAt).ThenByDescending(d => d.Id)
                : resultQuery.OrderBy(d => d.UpdatedAt).ThenBy(d => d.Id);
        }

        return true;
    }

    public static string EncodeCursor(this Driver driver, string sortField)
    {
        DateTimeOffset timestamp = sortField == "updatedAt" ? driver.UpdatedAt : driver.CreatedAt;
        DriverCursor cursor = new(timestamp, driver.Id);
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(cursor);
        return Convert.ToBase64String(jsonBytes);
    }

    private static bool TryDecodeCursor(string pageToken, out DriverCursor cursor)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(pageToken);
            cursor = JsonSerializer.Deserialize<DriverCursor>(bytes);
            return cursor.LastId != Guid.Empty;
        }
        catch
        {
            cursor = default;
            return false;
        }
    }

    public static string GetETag(this Driver driver)
    {
        long normalizedTicks = driver.UpdatedAt.UtcTicks / 1000;
        byte[] bytes = Encoding.UTF8.GetBytes($"{driver.Id:N}:{normalizedTicks}");
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..16];
        return $"\"v1-{hash}\"";
    }

    public static bool MatchesETag(this Driver driver, string? ifMatch)
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

        string currentETag = driver.GetETag();
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
