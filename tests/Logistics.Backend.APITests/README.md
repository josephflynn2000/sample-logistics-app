# Logistics API Conformance Tests (Bruno v4)

This directory contains the automated API conformance test suite for the Logistics Management REST API (`v1`), built for **Bruno v4** using the OpenCollection (`opencollection: 1.0.0`) YAML format and synchronized with [`docs/openapi/openapi.yaml`](../../docs/openapi/openapi.yaml).

## Prerequisites

- **Bruno Desktop App** `v4.x` (for interactive execution)
- **Bruno CLI (`@usebruno/cli`)** `v4.x` (for CLI and CI/CD automation):
  ```bash
  npm install -g @usebruno/cli
  ```

## Collection Architecture & Execution Order

The collection is located in [`Logistics.Backend.APITests/`](Logistics.Backend.APITests/) and organized into three sequential resource folders (`Vehicles` -> `Drivers` -> `Routes`). Each folder executes a self-contained lifecycle:

### 1. `Vehicles/` (`seq: 1`)
1. `Register a new vehicle.yml` (`POST /vehicles` -> `201 Created`): Generates an `Idempotency-Key`, validates `Location`, `ETag`, and the OpenAPI `Vehicle` JSON schema, and stores `vehicleId` and `vehicleEtag`.
2. `List vehicles.yml` (`GET /vehicles` -> `200 OK`): Validates paginated `VehicleListResponse` schema and verifies the created vehicle appears in `items`.
3. `Register duplicate vehicle - 409 Conflict.yml` (`POST /vehicles` -> `409 Conflict`): Verifies duplicate VIN/license plate detection returns `application/problem+json` with `https://problems-registry.smartbear.com/already-exists/`.
4. `Get a vehicle by ID.yml` (`GET /vehicles/:vehicleId` -> `200 OK`): Validates `ETag` and `Vehicle` schema.
5. `Replace a vehicle.yml` (`PUT /vehicles/:vehicleId` -> `200 OK`): Sends `If-Match: {{vehicleEtag}}`, verifies updated fields and new `ETag`.
6. `Partially update a vehicle.yml` (`PATCH /vehicles/:vehicleId` -> `200 OK`): Sends `If-Match: {{vehicleEtag}}`, transitions status to `InMaintenance`, and updates `vehicleEtag`.
7. `Replace vehicle with stale ETag - 412 Precondition Failed.yml` (`PUT /vehicles/:vehicleId` -> `412 Precondition Failed`): Sends a stale `If-Match` header and validates `application/problem+json` with `https://problems-registry.smartbear.com/business-rule-violation/`.
8. `Register vehicle - 400 Validation Error.yml` (`POST /vehicles` -> `400 Bad Request`): Submits invalid VIN and negative capacity, verifying `application/problem+json` with `https://problems-registry.smartbear.com/validation-error/` and field-level `errors[]`.
9. `Delete a vehicle.yml` (`DELETE /vehicles/:vehicleId` -> `204 No Content`): Deletes the vehicle using `If-Match: {{vehicleEtag}}`.
10. `Get deleted vehicle - 404 Not Found.yml` (`GET /vehicles/:vehicleId` -> `404 Not Found`): Confirms deletion returns `application/problem+json` with `https://problems-registry.smartbear.com/not-found/`.

### 2. `Drivers/` (`seq: 2`)
1. `Register a new driver.yml` (`POST /drivers` -> `201 Created`): Captures `driverId` and `driverEtag`, validates `Driver` schema.
2. `List drivers.yml` (`GET /drivers` -> `200 OK`): Validates `DriverListResponse` schema.
3. `Register duplicate driver - 409 Conflict.yml` (`POST /drivers` -> `409 Conflict`): Validates `already-exists` problem details.
4. `Get a driver by ID.yml` (`GET /drivers/:driverId` -> `200 OK`): Validates `Driver` schema and `ETag`.
5. `Replace a driver.yml` (`PUT /drivers/:driverId` -> `200 OK`): Updates driver attributes with `If-Match: {{driverEtag}}`.
6. `Partially update a driver.yml` (`PATCH /drivers/:driverId` -> `200 OK`): Updates driver status to `OnLeave` with `If-Match: {{driverEtag}}`.
7. `Replace driver with stale ETag - 412 Precondition Failed.yml` (`PUT /drivers/:driverId` -> `412 Precondition Failed`): Validates `business-rule-violation` problem details.
8. `Register driver - 400 Validation Error.yml` (`POST /drivers` -> `400 Bad Request`): Validates `validation-error` problem details and `errors[]`.
9. `Delete a driver.yml` (`DELETE /drivers/:driverId` -> `204 No Content`): Deletes the driver.
10. `Get deleted driver - 404 Not Found.yml` (`GET /drivers/:driverId` -> `404 Not Found`): Validates `not-found` problem details.

### 3. `Routes/` (`seq: 3`)
1. `Create a new route.yml` (`POST /routes` -> `201 Created`): Creates a route with 3 ordered waypoints, captures `routeId` and `routeEtag`, and validates `Route` and `Waypoint` schemas.
2. `List routes.yml` (`GET /routes` -> `200 OK`): Validates `RouteListResponse` schema.
3. `Create duplicate route - 409 Conflict.yml` (`POST /routes` -> `409 Conflict`): Validates `already-exists` problem details.
4. `Get a route by ID.yml` (`GET /routes/:routeId` -> `200 OK`): Validates `Route` schema and ordered waypoints.
5. `Replace a route.yml` (`PUT /routes/:routeId` -> `200 OK`): Replaces route and embedded waypoints with `If-Match: {{routeEtag}}`.
6. `Partially update a route.yml` (`PATCH /routes/:routeId` -> `200 OK`): Updates route status with `If-Match: {{routeEtag}}`.
7. `Get ordered waypoints for a route.yml` (`GET /routes/:routeId/waypoints` -> `200 OK`): Validates `RouteWaypointsResponse` schema and ascending waypoint sequence order.
8. `Replace ordered waypoints for a route.yml` (`PUT /routes/:routeId/waypoints` -> `200 OK`): Atomically replaces the route's ordered waypoints with `If-Match: {{routeEtag}}`.
9. `Replace route with stale ETag - 412 Precondition Failed.yml` (`PUT /routes/:routeId` -> `412 Precondition Failed`): Validates `business-rule-violation` problem details.
10. `Create route - 400 Validation Error.yml` (`POST /routes` -> `400 Bad Request`): Submits fewer than 2 waypoints and out-of-range WGS84 coordinates, validating `validation-error` problem details.
11. `Delete a route.yml` (`DELETE /routes/:routeId` -> `204 No Content`): Deletes the route.
12. `Get deleted route - 404 Not Found.yml` (`GET /routes/:routeId` -> `404 Not Found`): Validates `not-found` problem details.

## Running the Tests

### Using the Automation Script

Run the entire suite against the default `local` environment (`http://localhost:5000/v1`):

```bash
./tests/Logistics.Backend.APITests/run-api-tests.sh
```

Override the target API base URL and generate JUnit XML, JSON, and HTML reports:

```bash
./tests/Logistics.Backend.APITests/run-api-tests.sh \
  --base-url http://localhost:8080/v1 \
  --reports
```

Run only a specific resource folder or tag:

```bash
# Run only Vehicles folder
./tests/Logistics.Backend.APITests/run-api-tests.sh --folder Vehicles

# Run only RFC 9457 error contract tests
./tests/Logistics.Backend.APITests/run-api-tests.sh --tags ErrorContract
```

### Direct Bruno CLI Commands

From `tests/Logistics.Backend.APITests/Logistics.Backend.APITests`:

```bash
# Run all 32 requests recursively with the local environment
bru run -r --env local

# Override baseUrl at runtime without modifying environments/local.yml
bru run -r --env local --env-var baseUrl=http://localhost:5000/v1

# Generate JUnit XML report for CI pipelines
bru run -r --env local --reporter-junit reports/junit.xml
```
