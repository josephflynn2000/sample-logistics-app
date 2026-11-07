using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Logistics.Backend.Infrastructure;
using Logistics.Backend.Model.Routes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using RouteDto = Logistics.Backend.Model.Routes.Route;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Logistics.Backend.IntegrationTests;

[TestClass]
public sealed class RouteStopsPersistenceTests
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"logistics-test-{Guid.NewGuid():N}.db");

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Initialize()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:LogisticsDatabase", $"Data Source={_dbPath}");
            });

        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        SqliteConnection.ClearAllPools();

        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string path = _dbPath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [TestMethod]
    public async Task CreateRoute_PersistsStopsInCollectionIndexOrder_RegardlessOfInputSequenceNumber()
    {
        CreateRouteRequest request = new()
        {
            Code = "RT-IDX-01",
            Name = "Chicago to Minneapolis",
            Description = "Collection index ordering test",
            Status = RouteStatus.Active,
            EstimatedDistanceKm = 650,
            EstimatedDurationMinutes = 390,
            Waypoints =
            [
                new RouteWaypoint
                {
                    SequenceNumber = 99,
                    Label = "Chicago Origin",
                    Address = "2800 S Ashland Ave, Chicago, IL",
                    Latitude = 41.8414,
                    Longitude = -87.6658,
                    StopType = StopType.Origin,
                    DwellTimeMinutes = 20
                },
                new RouteWaypoint
                {
                    SequenceNumber = 99,
                    Label = "Madison Cross-Dock",
                    Address = "4401 Lien Rd, Madison, WI",
                    Latitude = 43.1165,
                    Longitude = -89.3065,
                    StopType = StopType.Waypoint,
                    DwellTimeMinutes = 30
                },
                new RouteWaypoint
                {
                    SequenceNumber = 1,
                    Label = "Minneapolis Hub",
                    Address = "1900 Broadway St NE, Minneapolis, MN",
                    Latitude = 45.0031,
                    Longitude = -93.2492,
                    StopType = StopType.Destination,
                    DwellTimeMinutes = 15
                }
            ]
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/routes", request);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        RouteDto? created = await response.Content.ReadFromJsonAsync<RouteDto>();
        Assert.IsNotNull(created);
        Assert.HasCount(3, created.Waypoints);
        CollectionAssert.AreEqual(
            new[] { 1, 2, 3 },
            created.Waypoints.Select(w => w.SequenceNumber).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Chicago Origin", "Madison Cross-Dock", "Minneapolis Hub" },
            created.Waypoints.Select(w => w.Label).ToArray());

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        LogisticsContext db = scope.ServiceProvider.GetRequiredService<LogisticsContext>();

        List<RouteWaypoint> persistedStops = await db.Routes
            .AsNoTracking()
            .Where(r => r.Id == created.Id)
            .SelectMany(r => r.Waypoints)
            .OrderBy(s => s.SequenceNumber)
            .ToListAsync();

        Assert.HasCount(3, persistedStops);
        CollectionAssert.AreEqual(
            new[] { 1, 2, 3 },
            persistedStops.Select(s => s.SequenceNumber).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Chicago Origin", "Madison Cross-Dock", "Minneapolis Hub" },
            persistedStops.Select(s => s.Label).ToArray());
    }

    [TestMethod]
    public async Task CreateRoute_RejectsTooFewStops_AndWritesNothing()
    {
        CreateRouteRequest singleStopRequest = new()
        {
            Code = "RT-ONE-01",
            Name = "Single Stop Route",
            Waypoints =
            [
                new RouteWaypoint
                {
                    Label = "Only Stop",
                    Latitude = 41.84,
                    Longitude = -87.66,
                    StopType = StopType.Origin,
                    DwellTimeMinutes = 10
                }
            ]
        };

        HttpResponseMessage oneStopResponse = await _client.PostAsJsonAsync("/v1/routes", singleStopRequest);
        Assert.AreEqual(HttpStatusCode.BadRequest, oneStopResponse.StatusCode);

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        LogisticsContext db = scope.ServiceProvider.GetRequiredService<LogisticsContext>();
        Assert.AreEqual(0, await db.Routes.CountAsync());
        int totalStops = await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM \"RouteStops\"")
            .SingleAsync();
        Assert.AreEqual(0, totalStops);
    }

    [TestMethod]
    public async Task ReplaceAndWaypointsPut_ShrinkAndGrowStops_WithoutOrphansOrDuplicates()
    {
        CreateRouteRequest createRequest = new()
        {
            Code = "RT-MUT-01",
            Name = "Mutable Corridor",
            Status = RouteStatus.Draft,
            Waypoints = CreateSampleWaypoints(4)
        };

        HttpResponseMessage createRes = await _client.PostAsJsonAsync("/v1/routes", createRequest);
        Assert.AreEqual(HttpStatusCode.Created, createRes.StatusCode);
        RouteDto created = (await createRes.Content.ReadFromJsonAsync<RouteDto>())!;

        // 1. Shrink from 4 stops to 2 stops via PUT /v1/routes/{id}
        UpdateRouteRequest shrinkUpdate = new()
        {
            Code = "RT-MUT-01",
            Name = "Mutable Corridor (Shrunk)",
            Status = RouteStatus.Active,
            EstimatedDistanceKm = 200,
            EstimatedDurationMinutes = 150,
            Waypoints = CreateSampleWaypoints(2, labelPrefix: "Shrunk")
        };

        HttpResponseMessage shrinkRes = await _client.PutAsJsonAsync($"/v1/routes/{created.Id}", shrinkUpdate);
        Assert.AreEqual(HttpStatusCode.OK, shrinkRes.StatusCode);
        RouteDto shrunk = (await shrinkRes.Content.ReadFromJsonAsync<RouteDto>())!;
        Assert.HasCount(2, shrunk.Waypoints);
        CollectionAssert.AreEqual(
            new[] { "Shrunk #1", "Shrunk #2" },
            shrunk.Waypoints.Select(w => w.Label).ToArray());

        await AssertDatabaseStopCountAsync(created.Id, expectedCount: 2);

        // 2. Grow from 2 stops to 5 stops via PUT /v1/routes/{id}/waypoints
        ReplaceWaypointsRequest growWaypoints = new()
        {
            Waypoints = CreateSampleWaypoints(5, labelPrefix: "Grown")
        };

        HttpResponseMessage growRes = await _client.PutAsJsonAsync(
            $"/v1/routes/{created.Id}/waypoints",
            growWaypoints);
        Assert.AreEqual(HttpStatusCode.OK, growRes.StatusCode);
        RouteWaypointsResponse grown = (await growRes.Content.ReadFromJsonAsync<RouteWaypointsResponse>())!;
        Assert.HasCount(5, grown.Waypoints);
        CollectionAssert.AreEqual(
            new[] { 1, 2, 3, 4, 5 },
            grown.Waypoints.Select(w => w.SequenceNumber).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Grown #1", "Grown #2", "Grown #3", "Grown #4", "Grown #5" },
            grown.Waypoints.Select(w => w.Label).ToArray());

        await AssertDatabaseStopCountAsync(created.Id, expectedCount: 5);
    }

    [TestMethod]
    public async Task RoutesSharingIdenticalCoordinates_CascadeDeleteIndependently()
    {
        List<RouteWaypoint> sharedCoordinates =
        [
            new RouteWaypoint
            {
                SequenceNumber = 1,
                Label = "Shared Depot Origin",
                Address = "100 Terminal Way",
                Latitude = 41.8800,
                Longitude = -87.6300,
                StopType = StopType.Origin,
                DwellTimeMinutes = 15
            },
            new RouteWaypoint
            {
                SequenceNumber = 2,
                Label = "Shared Hub Destination",
                Address = "200 Terminal Way",
                Latitude = 42.3300,
                Longitude = -83.0400,
                StopType = StopType.Destination,
                DwellTimeMinutes = 25
            }
        ];

        HttpResponseMessage resA = await _client.PostAsJsonAsync("/v1/routes", new CreateRouteRequest
        {
            Code = "RT-SHARE-A",
            Name = "Shared Corridor A",
            Waypoints = sharedCoordinates
        });
        HttpResponseMessage resB = await _client.PostAsJsonAsync("/v1/routes", new CreateRouteRequest
        {
            Code = "RT-SHARE-B",
            Name = "Shared Corridor B",
            Waypoints = sharedCoordinates
        });

        Assert.AreEqual(HttpStatusCode.Created, resA.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, resB.StatusCode);

        RouteDto routeA = (await resA.Content.ReadFromJsonAsync<RouteDto>())!;
        RouteDto routeB = (await resB.Content.ReadFromJsonAsync<RouteDto>())!;
        EntityTagHeaderValue? etagB = resB.Headers.ETag;
        Assert.IsNotNull(etagB);

        // Unconditional delete of Route A (ExecuteDeleteAsync path) -> cascades Route A stops only
        HttpResponseMessage delA = await _client.DeleteAsync($"/v1/routes/{routeA.Id}");
        Assert.AreEqual(HttpStatusCode.NoContent, delA.StatusCode);

        await AssertDatabaseStopCountAsync(routeA.Id, expectedCount: 0);
        await AssertDatabaseStopCountAsync(routeB.Id, expectedCount: 2);

        // Conditional If-Match delete of Route B (tracked Remove path) -> cascades Route B stops
        using HttpRequestMessage delBReq = new(HttpMethod.Delete, $"/v1/routes/{routeB.Id}");
        delBReq.Headers.IfMatch.Add(etagB);
        HttpResponseMessage delB = await _client.SendAsync(delBReq);
        Assert.AreEqual(HttpStatusCode.NoContent, delB.StatusCode);

        await AssertDatabaseStopCountAsync(routeB.Id, expectedCount: 0);
    }

    [TestMethod]
    public async Task DatabasePrimaryKeyAndForeignKeyConstraints_RejectDuplicateSlotOrOrphanRows()
    {
        HttpResponseMessage createRes = await _client.PostAsJsonAsync("/v1/routes", new CreateRouteRequest
        {
            Code = "RT-CHK-01",
            Name = "Constraint Verification Route",
            Waypoints = CreateSampleWaypoints(2)
        });
        RouteDto created = (await createRes.Content.ReadFromJsonAsync<RouteDto>())!;

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        LogisticsContext db = scope.ServiceProvider.GetRequiredService<LogisticsContext>();

        // Duplicate (RouteId, SequenceNumber) violates PK_RouteStops
        await Assert.ThrowsExactlyAsync<SqliteException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"RouteStops\" (\"RouteId\", \"SequenceNumber\", \"Label\", \"Latitude\", \"Longitude\", \"StopType\", \"DwellTimeMinutes\") VALUES ({created.Id}, 1, 'Duplicate Slot', 40.0, -80.0, 0, 0)"));

        // Nonexistent RouteId violates FK_RouteStops_Routes_RouteId
        Guid missingRouteId = Guid.NewGuid();
        await Assert.ThrowsExactlyAsync<SqliteException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"RouteStops\" (\"RouteId\", \"SequenceNumber\", \"Label\", \"Latitude\", \"Longitude\", \"StopType\", \"DwellTimeMinutes\") VALUES ({missingRouteId}, 1, 'Orphan', 40.0, -80.0, 0, 0)"));
    }

    private async Task AssertDatabaseStopCountAsync(Guid routeId, int expectedCount)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        LogisticsContext db = scope.ServiceProvider.GetRequiredService<LogisticsContext>();

        List<int> sequences = await db.Database
            .SqlQuery<int>($"SELECT \"SequenceNumber\" AS \"Value\" FROM \"RouteStops\" WHERE \"RouteId\" = {routeId} ORDER BY \"SequenceNumber\"")
            .ToListAsync();

        Assert.HasCount(expectedCount, sequences);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, expectedCount).ToArray(),
            sequences);
    }

    private static List<RouteWaypoint> CreateSampleWaypoints(int count, string labelPrefix = "Stop")
    {
        List<RouteWaypoint> list = new(count);
        for (int i = 1; i <= count; i++)
        {
            list.Add(new RouteWaypoint
            {
                SequenceNumber = i,
                Label = $"{labelPrefix} #{i}",
                Address = $"{i * 100} Freight Blvd",
                Latitude = 41.0 + (i * 0.25),
                Longitude = -87.0 - (i * 0.25),
                StopType = i == 1
                    ? StopType.Origin
                    : i == count
                        ? StopType.Destination
                        : StopType.Waypoint,
                DwellTimeMinutes = i * 10
            });
        }

        return list;
    }
}
