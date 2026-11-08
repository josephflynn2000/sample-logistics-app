using System.ComponentModel;
using Asp.Versioning.Builder;
using Logistics.Backend.Extensions;
using Logistics.Backend.Infrastructure;
using Logistics.Backend.Model.Routes;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using RouteEntity = Logistics.Backend.Model.Routes.Route;

namespace Logistics.Backend.Apis;

/// <summary>
/// Operations for defining and managing transit/delivery routes and their ordered waypoints.
/// </summary>
public static class RouteApi
{
    public static void MapRouteApi(this IEndpointRouteBuilder app)
    {
        IVersionedEndpointRouteBuilder versionSet = app
            .NewVersionedApi("Route API");
        RouteGroupBuilder group = versionSet
            .MapGroup("/v{version:apiVersion}/routes")
            .HasApiVersion(1, 0)
            .WithTags("Routes");

        group.MapPost("/", CreateRoute)
            .WithName("Routes_Create")
            .WithSummary("Create a new route")
            .WithDescription("Registers a new transit or delivery route along with its ordered sequence of geographic waypoints (`waypoints`). Returns `201 Created` with `Location` and `ETag` headers.")
            .Accepts<CreateRouteRequest>("application/json")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/", ListRoutes)
            .WithName("Routes_List")
            .WithSummary("List routes")
            .WithDescription("Retrieves a paginated list of routes, with optional filtering by lifecycle status or search query against route code and name.")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/{routeId}", GetRouteById)
            .WithName("Routes_Get")
            .WithSummary("Get a route by ID")
            .WithDescription("Retrieves the full details of a route, including its ordered list of waypoints.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPut("/{routeId}", ReplaceRoute)
            .WithName("Routes_Replace")
            .WithSummary("Replace a route")
            .WithDescription("Performs a full replacement of an existing route and its embedded waypoints.")
            .Accepts<UpdateRouteRequest>("application/json")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPatch("/{routeId}", PatchRoute)
            .WithName("Routes_Update")
            .WithSummary("Partially update a route")
            .WithDescription("Applies a partial update to an existing route's metadata or waypoints using JSON Merge Patch (RFC 7396) semantics.")
            .Accepts<PatchRouteRequest>("application/merge-patch+json", "application/json")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapDelete("/{routeId}", DeleteRoute)
            .WithName("Routes_Delete")
            .WithSummary("Delete a route")
            .WithDescription("Permanently removes a route from the registry.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        RouteGroupBuilder waypointsGroup = group.MapGroup("/{routeId}/waypoints");
        waypointsGroup.MapGet("/", GetRouteWaypoints)
            .WithName("Routes_GetWaypoints")
            .WithSummary("Get ordered waypoints for a route")
            .WithDescription("Retrieves the ordered sequence of geographic waypoints for a specific route.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        waypointsGroup.MapPut("/", ReplaceRouteWaypoints)
            .WithName("Routes_ReplaceWaypoints")
            .WithSummary("Replace ordered waypoints for a route")
            .WithDescription("Atomically replaces the entire ordered sequence of waypoints for a specific route.")
            .Accepts<ReplaceWaypointsRequest>("application/json")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    /// <response code="200">Paginated list of routes.</response>
    /// <response code="400">Request validation failed (`400 Bad Request`).</response>
    /// <response code="500">An unexpected server error occurred (`500 Internal Server Error`).</response>
    private static async Task<Results<Ok<RouteListResponse>, ProblemHttpResult>> ListRoutes(
        [AsParameters] QueryRouteRequest request,
        LogisticsContext dbContext,
        CancellationToken cancellationToken = default)
    {
        IQueryable<RouteEntity> filteredQuery = dbContext.Routes.AsNoTracking().ApplyFilters(request);

        if (!filteredQuery.TryApplyCursorAndOrdering(
                request.OrderBy,
                request.PageToken,
                out IQueryable<RouteEntity> pagedQuery,
                out string sortField))
        {
            return TypedResults.Problem(
                detail: "The specified orderBy or pageToken query parameter is invalid.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        int pageSize = request.PageSize ?? 25;
        List<RouteEntity> routes = await pagedQuery
            .AsSplitQuery()
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        int totalCount;
        if (string.IsNullOrWhiteSpace(request.PageToken) && routes.Count <= pageSize)
        {
            totalCount = routes.Count;
        }
        else
        {
            totalCount = await filteredQuery.CountAsync(cancellationToken);
        }

        string? nextPageToken = null;
        if (routes.Count > pageSize)
        {
            routes.RemoveAt(pageSize);
            nextPageToken = routes[^1].EncodeCursor(sortField);
        }

        var response = new RouteListResponse
        {
            Items = routes,
            TotalCount = totalCount,
            NextPageToken = nextPageToken
        };

        return TypedResults.Ok(response);
    }

    /// <response code="200">Route details.</response>
    /// <response code="404">The requested resource was not found (`404 Not Found`).</response>
    /// <response code="500">An unexpected server error occurred (`500 Internal Server Error`).</response>
    private static async Task<Results<Ok<RouteEntity>, ProblemHttpResult>> GetRouteById(
        [Description("Unique identifier (UUID) of the route.")] Guid routeId,
        LogisticsContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        RouteEntity? route = await dbContext.Routes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == routeId, cancellationToken);

        if (route is null)
        {
            return TypedResults.Problem(
                detail: "The requested route was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        httpContext.Response.Headers.ETag = route.GetETag();
        return TypedResults.Ok(route);
    }

    /// <response code="201">Route successfully registered.</response>
    /// <response code="400">Request validation failed (`400 Bad Request`).</response>
    /// <response code="409">A conflict occurred with the current state of the target resource (`409 Conflict`), such as a duplicate unique identifier (VIN, license plate, employee number, or route code).</response>
    /// <response code="500">An unexpected server error occurred (`500 Internal Server Error`).</response>
    private static async Task<Results<Created<RouteEntity>, ProblemHttpResult>> CreateRoute(
        CreateRouteRequest createRoute,
        LogisticsContext dbContext,
        IMemoryCache memoryCache,
        HttpContext httpContext,
        [FromHeader(Name = "Idempotency-Key"), Description("Optional client-generated unique key (e.g., UUID) to guarantee idempotent resource creation across network retries.")] Guid? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        string? cacheKey = idempotencyKey.HasValue
            ? $"routes:idempotency:{idempotencyKey.Value:N}"
            : null;

        if (cacheKey is not null &&
            memoryCache.TryGetValue(cacheKey, out RouteEntity? cachedRoute) &&
            cachedRoute is not null)
        {
            httpContext.Response.Headers.ETag = cachedRoute.GetETag();
            return TypedResults.Created($"/v1/routes/{cachedRoute.Id}", cachedRoute);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        RouteEntity route = new()
        {
            Id = Guid.NewGuid(),
            Code = createRoute.Code,
            Name = createRoute.Name,
            Description = createRoute.Description,
            Status = createRoute.Status ?? RouteStatus.Draft,
            EstimatedDistanceKm = createRoute.EstimatedDistanceKm,
            EstimatedDurationMinutes = createRoute.EstimatedDurationMinutes,
            Waypoints = createRoute.Waypoints.IndexByPosition(),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Routes.Add(route);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueCodeViolation())
        {
            return TypedResults.Problem(
                detail: "A route with the same code already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (cacheKey is not null)
        {
            memoryCache.Set(cacheKey, route, TimeSpan.FromHours(24));
        }

        httpContext.Response.Headers.ETag = route.GetETag();
        return TypedResults.Created($"/v1/routes/{route.Id}", route);
    }

    /// <response code="200">Route successfully replaced.</response>
    /// <response code="400">Request validation failed (`400 Bad Request`).</response>
    /// <response code="404">The requested resource was not found (`404 Not Found`).</response>
    /// <response code="409">A conflict occurred with the current state of the target resource (`409 Conflict`), such as a duplicate unique identifier (VIN, license plate, employee number, or route code).</response>
    /// <response code="412">The `If-Match` precondition failed (`412 Precondition Failed`) because the resource was modified by another request.</response>
    /// <response code="500">An unexpected server error occurred (`500 Internal Server Error`).</response>
    private static async Task<Results<Ok<RouteEntity>, ProblemHttpResult>> ReplaceRoute(
        [Description("Unique identifier (UUID) of the route.")] Guid routeId,
        UpdateRouteRequest updateRoute,
        LogisticsContext dbContext,
        HttpContext httpContext,
        [FromHeader(Name = "If-Match"), Description("Optional entity tag (`ETag`) value for optimistic concurrency control. If the resource has been modified since this `ETag` was issued, the server returns `412 Precondition Failed`.")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        RouteEntity? route = await dbContext.Routes.FindAsync([routeId], cancellationToken);
        if (route is null)
        {
            return TypedResults.Problem(
                detail: "The requested route was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!route.MatchesETag(ifMatch))
        {
            return TypedResults.Problem(
                detail: "The resource ETag does not match the provided If-Match header value.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        bool isUnconditional = string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*";
        List<RouteWaypoint> indexedWaypoints = updateRoute.Waypoints.IndexByPosition();

        for (int attempt = 0; ; attempt++)
        {
            route.Code = updateRoute.Code;
            route.Name = updateRoute.Name;
            route.Description = updateRoute.Description;
            route.Status = updateRoute.Status;
            route.EstimatedDistanceKm = updateRoute.EstimatedDistanceKm;
            route.EstimatedDurationMinutes = updateRoute.EstimatedDurationMinutes;
            route.Waypoints = indexedWaypoints;
            route.UpdatedAt = route.NextUpdatedAt();

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (isUnconditional && attempt < 2)
            {
                dbContext.ChangeTracker.Clear();
                route = await dbContext.Routes.FindAsync([routeId], cancellationToken);
                if (route is null)
                {
                    return TypedResults.Problem(
                        detail: "The requested route was not found.",
                        statusCode: StatusCodes.Status404NotFound);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return TypedResults.Problem(
                    detail: "The resource was modified or deleted by another concurrent request.",
                    statusCode: StatusCodes.Status412PreconditionFailed);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueCodeViolation())
            {
                return TypedResults.Problem(
                    detail: "A route with the same code already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        httpContext.Response.Headers.ETag = route.GetETag();
        return TypedResults.Ok(route);
    }

    /// <response code="200">Route successfully updated.</response>
    /// <response code="400">Request validation failed (`400 Bad Request`).</response>
    /// <response code="404">The requested resource was not found (`404 Not Found`).</response>
    /// <response code="409">A conflict occurred with the current state of the target resource (`409 Conflict`), such as a duplicate unique identifier (VIN, license plate, employee number, or route code).</response>
    /// <response code="412">The `If-Match` precondition failed (`412 Precondition Failed`) because the resource was modified by another request.</response>
    /// <response code="500">An unexpected server error occurred (`500 Internal Server Error`).</response>
    private static async Task<Results<Ok<RouteEntity>, ProblemHttpResult>> PatchRoute(
        [Description("Unique identifier (UUID) of the route.")] Guid routeId,
        PatchRouteRequest patchRoute,
        LogisticsContext dbContext,
        HttpContext httpContext,
        [FromHeader(Name = "If-Match"), Description("Optional entity tag (`ETag`) value for optimistic concurrency control. If the resource has been modified since this `ETag` was issued, the server returns `412 Precondition Failed`.")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        RouteEntity? route = await dbContext.Routes.FindAsync([routeId], cancellationToken);
        if (route is null)
        {
            return TypedResults.Problem(
                detail: "The requested route was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!route.MatchesETag(ifMatch))
        {
            return TypedResults.Problem(
                detail: "The resource ETag does not match the provided If-Match header value.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        bool isUnconditional = string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*";
        List<RouteWaypoint>? indexedWaypoints = patchRoute.Waypoints?.IndexByPosition();

        for (int attempt = 0; ; attempt++)
        {
            if (patchRoute.Code is not null) route.Code = patchRoute.Code;
            if (patchRoute.Name is not null) route.Name = patchRoute.Name;
            if (patchRoute.Description is not null) route.Description = patchRoute.Description;
            if (patchRoute.Status.HasValue) route.Status = patchRoute.Status.Value;
            if (patchRoute.EstimatedDistanceKm.HasValue) route.EstimatedDistanceKm = patchRoute.EstimatedDistanceKm.Value;
            if (patchRoute.EstimatedDurationMinutes.HasValue) route.EstimatedDurationMinutes = patchRoute.EstimatedDurationMinutes.Value;
            if (indexedWaypoints is not null) route.Waypoints = indexedWaypoints;

            route.UpdatedAt = route.NextUpdatedAt();

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (isUnconditional && attempt < 2)
            {
                dbContext.ChangeTracker.Clear();
                route = await dbContext.Routes.FindAsync([routeId], cancellationToken);
                if (route is null)
                {
                    return TypedResults.Problem(
                        detail: "The requested route was not found.",
                        statusCode: StatusCodes.Status404NotFound);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return TypedResults.Problem(
                    detail: "The resource was modified or deleted by another concurrent request.",
                    statusCode: StatusCodes.Status412PreconditionFailed);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueCodeViolation())
            {
                return TypedResults.Problem(
                    detail: "A route with the same code already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        httpContext.Response.Headers.ETag = route.GetETag();
        return TypedResults.Ok(route);
    }

    /// <response code="204">Route successfully deleted.</response>
    /// <response code="404">The requested resource was not found (`404 Not Found`).</response>
    /// <response code="412">The `If-Match` precondition failed (`412 Precondition Failed`) because the resource was modified by another request.</response>
    /// <response code="500">An unexpected server error occurred (`500 Internal Server Error`).</response>
    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteRoute(
        [Description("Unique identifier (UUID) of the route.")] Guid routeId,
        LogisticsContext dbContext,
        [FromHeader(Name = "If-Match"), Description("Optional entity tag (`ETag`) value for optimistic concurrency control. If the resource has been modified since this `ETag` was issued, the server returns `412 Precondition Failed`.")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*")
        {
            int deleted = await dbContext.Routes
                .Where(r => r.Id == routeId)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted == 0)
            {
                return TypedResults.Problem(
                    detail: "The requested route was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return TypedResults.NoContent();
        }

        RouteEntity? route = await dbContext.Routes.FindAsync([routeId], cancellationToken);
        if (route is null)
        {
            return TypedResults.Problem(
                detail: "The requested route was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!route.MatchesETag(ifMatch))
        {
            return TypedResults.Problem(
                detail: "The resource ETag does not match the provided If-Match header value.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        dbContext.Routes.Remove(route);
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

    /// <response code="200">Ordered sequence of waypoints for the route.</response>
    /// <response code="404">The requested resource was not found (`404 Not Found`).</response>
    /// <response code="500">An unexpected server error occurred (`500 Internal Server Error`).</response>
    private static async Task<Results<Ok<RouteWaypointsResponse>, ProblemHttpResult>> GetRouteWaypoints(
        [Description("Unique identifier (UUID) of the route.")] Guid routeId,
        LogisticsContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        RouteEntity? route = await dbContext.Routes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == routeId, cancellationToken);

        if (route is null)
        {
            return TypedResults.Problem(
                detail: "The requested route was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        httpContext.Response.Headers.ETag = route.GetETag();
        var response = new RouteWaypointsResponse
        {
            RouteId = route.Id,
            Waypoints = [.. route.Waypoints.OrderBy(static w => w.SequenceNumber)]
        };

        return TypedResults.Ok(response);
    }

    /// <response code="200">Ordered waypoints successfully replaced.</response>
    /// <response code="400">Request validation failed (`400 Bad Request`).</response>
    /// <response code="404">The requested resource was not found (`404 Not Found`).</response>
    /// <response code="412">The `If-Match` precondition failed (`412 Precondition Failed`) because the resource was modified by another request.</response>
    /// <response code="500">An unexpected server error occurred (`500 Internal Server Error`).</response>
    private static async Task<Results<Ok<RouteWaypointsResponse>, ProblemHttpResult>> ReplaceRouteWaypoints(
        [Description("Unique identifier (UUID) of the route.")] Guid routeId,
        ReplaceWaypointsRequest replaceWaypoints,
        LogisticsContext dbContext,
        HttpContext httpContext,
        [FromHeader(Name = "If-Match"), Description("Optional entity tag (`ETag`) value for optimistic concurrency control. If the resource has been modified since this `ETag` was issued, the server returns `412 Precondition Failed`.")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        RouteEntity? route = await dbContext.Routes.FindAsync([routeId], cancellationToken);
        if (route is null)
        {
            return TypedResults.Problem(
                detail: "The requested route was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!route.MatchesETag(ifMatch))
        {
            return TypedResults.Problem(
                detail: "The resource ETag does not match the provided If-Match header value.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        bool isUnconditional = string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*";
        List<RouteWaypoint> indexedWaypoints = replaceWaypoints.Waypoints.IndexByPosition();

        for (int attempt = 0; ; attempt++)
        {
            route.Waypoints = indexedWaypoints;
            route.UpdatedAt = route.NextUpdatedAt();

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (isUnconditional && attempt < 2)
            {
                dbContext.ChangeTracker.Clear();
                route = await dbContext.Routes.FindAsync([routeId], cancellationToken);
                if (route is null)
                {
                    return TypedResults.Problem(
                        detail: "The requested route was not found.",
                        statusCode: StatusCodes.Status404NotFound);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return TypedResults.Problem(
                    detail: "The resource was modified or deleted by another concurrent request.",
                    statusCode: StatusCodes.Status412PreconditionFailed);
            }
        }

        httpContext.Response.Headers.ETag = route.GetETag();
        var response = new RouteWaypointsResponse
        {
            RouteId = route.Id,
            Waypoints = route.Waypoints
        };

        return TypedResults.Ok(response);
    }
}