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

    private static async Task<Results<Ok<RouteListResponse>, ProblemHttpResult>> ListRoutes(
        LogisticsContext dbContext,
        [AsParameters] QueryRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        IQueryable<RouteEntity> filteredQuery = dbContext.Routes.AsNoTracking().ApplyFilters(request);

        int totalCount = await filteredQuery.CountAsync(cancellationToken);

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
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

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

    private static async Task<Results<Ok<RouteEntity>, ProblemHttpResult>> GetRouteById(
        Guid routeId,
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

    private static async Task<Results<Created<RouteEntity>, ProblemHttpResult>> CreateRoute(
        CreateRouteRequest createRoute,
        LogisticsContext dbContext,
        IMemoryCache memoryCache,
        HttpContext httpContext,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey = null,
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

        bool exists = await dbContext.Routes
            .AsNoTracking()
            .AnyAsync(r => r.Code == createRoute.Code, cancellationToken);

        if (exists)
        {
            return TypedResults.Problem(
                detail: "A route with the same code already exists.",
                statusCode: StatusCodes.Status409Conflict);
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
            Waypoints = [.. createRoute.Waypoints.OrderBy(w => w.SequenceNumber)],
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (cacheKey is not null)
        {
            memoryCache.Set(cacheKey, route, TimeSpan.FromHours(24));
        }

        httpContext.Response.Headers.ETag = route.GetETag();
        return TypedResults.Created($"/v1/routes/{route.Id}", route);
    }

    private static async Task<Results<Ok<RouteEntity>, ProblemHttpResult>> ReplaceRoute(
        Guid routeId,
        UpdateRouteRequest updateRoute,
        LogisticsContext dbContext,
        HttpContext httpContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
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

        bool hasConflict = await dbContext.Routes
            .AsNoTracking()
            .AnyAsync(
                r => r.Id != routeId && r.Code == updateRoute.Code,
                cancellationToken);

        if (hasConflict)
        {
            return TypedResults.Problem(
                detail: "A route with the same code already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        route.Code = updateRoute.Code;
        route.Name = updateRoute.Name;
        route.Description = updateRoute.Description;
        route.Status = updateRoute.Status;
        route.EstimatedDistanceKm = updateRoute.EstimatedDistanceKm;
        route.EstimatedDurationMinutes = updateRoute.EstimatedDurationMinutes;
        route.Waypoints = [.. updateRoute.Waypoints.OrderBy(w => w.SequenceNumber)];
        route.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = route.GetETag();
        return TypedResults.Ok(route);
    }

    private static async Task<Results<Ok<RouteEntity>, ProblemHttpResult>> PatchRoute(
        Guid routeId,
        PatchRouteRequest patchRoute,
        LogisticsContext dbContext,
        HttpContext httpContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
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

        if (patchRoute.Code is not null && patchRoute.Code != route.Code)
        {
            bool codeConflict = await dbContext.Routes
                .AsNoTracking()
                .AnyAsync(
                    r => r.Id != routeId && r.Code == patchRoute.Code,
                    cancellationToken);

            if (codeConflict)
            {
                return TypedResults.Problem(
                    detail: "A route with the same code already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            route.Code = patchRoute.Code;
        }

        if (patchRoute.Name is not null) route.Name = patchRoute.Name;
        if (patchRoute.Description is not null) route.Description = patchRoute.Description;
        if (patchRoute.Status.HasValue) route.Status = patchRoute.Status.Value;
        if (patchRoute.EstimatedDistanceKm.HasValue) route.EstimatedDistanceKm = patchRoute.EstimatedDistanceKm.Value;
        if (patchRoute.EstimatedDurationMinutes.HasValue) route.EstimatedDurationMinutes = patchRoute.EstimatedDurationMinutes.Value;
        if (patchRoute.Waypoints is not null)
        {
            route.Waypoints = [.. patchRoute.Waypoints.OrderBy(w => w.SequenceNumber)];
        }

        route.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = route.GetETag();
        return TypedResults.Ok(route);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteRoute(
        Guid routeId,
        LogisticsContext dbContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
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

        dbContext.Routes.Remove(route);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<RouteWaypointsResponse>, ProblemHttpResult>> GetRouteWaypoints(
        Guid routeId,
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
            Waypoints = [.. route.Waypoints.OrderBy(w => w.SequenceNumber)]
        };

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<RouteWaypointsResponse>, ProblemHttpResult>> ReplaceRouteWaypoints(
        Guid routeId,
        ReplaceWaypointsRequest replaceWaypoints,
        LogisticsContext dbContext,
        HttpContext httpContext,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
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

        route.Waypoints = [.. replaceWaypoints.Waypoints.OrderBy(w => w.SequenceNumber)];
        route.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = route.GetETag();
        var response = new RouteWaypointsResponse
        {
            RouteId = route.Id,
            Waypoints = route.Waypoints
        };

        return TypedResults.Ok(response);
    }
}
