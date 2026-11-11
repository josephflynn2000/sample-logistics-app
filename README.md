# Logistics App

A sample logistics application.

## Scope

The initial scope is a CRUD (Create, Read, Update, Delete) application for managing core logistics entities:
- **Vehicles**: Manage fleet vehicles and their details.
- **Drivers**: Manage driver records and assignments.
- **Routes**: Define and manage delivery/transit routes.

## Architecture

At its simplest, the application consists of:
- **Web UI**: A web page that allows users to view and interact with logistics data.
- **Web API**: A backend service that serves the web page and exposes RESTful APIs for performing CRUD operations on vehicles, drivers, and routes.

## Running with .NET Aspire

The application uses [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) for distributed orchestration across the Angular frontend (`Frontend`), ASP.NET Core Web API (`Backend`), and database dependencies.

The database provider is dynamically selected based on the runtime environment:
- **Development**: Uses an embedded SQLite database (`./obj/app.db`) for fast local iteration without requiring container engines.
- **Staging / Production**: Provisions and connects to a containerized PostgreSQL database (`LogisticsDatabase`) managed by Aspire.

### Environments & Database Matrix

| Environment | Database Provider | Container Engine Required? | Target / Connection |
| :--- | :--- | :--- | :--- |
| **Development** (`Development`) | SQLite | No | `./obj/app.db` |
| **Staging** (`Staging`) | PostgreSQL | Yes (Docker / Podman) | Aspire PostgreSQL Container (`LogisticsDatabase`) |
| **Production** (`Production`) | PostgreSQL | Yes (Docker / Podman / Cloud) | Aspire PostgreSQL Container / Cloud DB (`LogisticsDatabase`) |

---

### 1. Development (Default)

Runs the Angular frontend and .NET backend locally with SQLite. No Docker or PostgreSQL installation required.

Using Aspire CLI:
```bash
aspire start
```

Or using the .NET CLI:
```bash
dotnet run --project src/Logistics.AppHost
```

---

### 2. Staging

Runs the application with containerized PostgreSQL orchestrated by Aspire. Aspire pulls and starts the PostgreSQL container, creates the `LogisticsDatabase`, injects the connection string into the Backend, and runs Entity Framework Core with the `Npgsql` PostgreSQL provider.

Using Aspire CLI:
```bash
DOTNET_ENVIRONMENT=Staging aspire start
```
*Or passing arguments to the AppHost:*
```bash
aspire start -- --environment Staging
```

Or using the .NET CLI:
```bash
dotnet run --project src/Logistics.AppHost --environment Staging
```

---

### 3. Production

Runs under production configuration with containerized PostgreSQL and production logging/settings.

Using Aspire CLI:
```bash
DOTNET_ENVIRONMENT=Production aspire start
```
*Or passing arguments to the AppHost:*
```bash
aspire start -- --environment Production
```

Or using the .NET CLI:
```bash
dotnet run --project src/Logistics.AppHost --environment Production
```

---

### Observability & Managing Resources

`Logistics.Backend` references `Logistics.ServiceDefaults` (`src/Logistics.ServiceDefaults`) along with the Aspire EF Core client integrations (`CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite` for Development and `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` for Staging/Production). This wires up:
- **OpenTelemetry Metrics**: ASP.NET Core request latency, Kestrel, `HttpClient`, `.NET Runtime` (GC, allocations, thread pool, lock contention), and database client metrics.
- **OpenTelemetry Distributed Tracing**: End-to-end HTTP request waterfalls with nested EF Core and database command spans.
- **Structured Logging**: OpenTelemetry log export correlated with active trace and span IDs.
- **Health Checks**: Readiness (`/health`, including database connectivity check) and liveness (`/alive`) probes.

Once Aspire starts:
- The **Aspire Dashboard** URL is displayed in the terminal (or opened in your browser), where you can view live OpenTelemetry traces, metrics, structured logs, and inspect resource states (`Frontend`, `Backend`, and `postgres` when in Staging/Production).
- Inspect running resources via CLI:
  ```bash
  aspire ps
  aspire describe
  ```
- Inspect OpenTelemetry traces, spans, and structured logs via CLI:
  ```bash
  aspire otel traces Backend
  aspire otel spans Backend --trace-id <trace-id>
  aspire otel logs Backend
  ```
- View real-time console logs from individual resources:
  ```bash
  aspire logs Backend
  aspire logs postgres
  ```
- Stop the Aspire orchestrator:
  ```bash
  aspire stop
  ```

#### Running `Logistics.Backend` Standalone

The backend also runs standalone without the Aspire AppHost:
```bash
dotnet run --project src/Logistics.Backend
```
To view OpenTelemetry traces, metrics, and logs when running `Logistics.Backend` standalone (e.g., during load testing of a `Release` build), launch the standalone Aspire Dashboard and pass `OTEL_EXPORTER_OTLP_ENDPOINT`:
```bash
aspire dashboard run
```

## API Specification & Testing

- **OpenAPI Specification**: [`docs/openapi/openapi.json`](docs/openapi/openapi.json) defines the v1 REST API contract for `/v1/vehicles`, `/v1/drivers`, and `/v1/routes` (including `/v1/routes/{routeId}/waypoints`), designed for Swagger tooling and DocFX documentation generation.
- **Bruno v4 API Conformance Suite**: [`tests/Logistics.Backend.APITests/`](tests/Logistics.Backend.APITests/README.md) contains 32 OpenCollection (`1.0.0`) YAML requests covering full CRUD lifecycles, optimistic concurrency (`ETag` / `If-Match`), and RFC 9457 Problem Details error contracts (`400`, `404`, `409`, `412`). Run via CLI:
  ```bash
  ./tests/Logistics.Backend.APITests/run-api-tests.sh --base-url http://localhost:5045/v1 --reports
  ```

## CI/CD Workflows

Pull Requests targeting `main` are validated by two stage-based GitHub Actions workflows:

### 1. Pull Request CI ([`.github/workflows/ci-pull-request.yml`](.github/workflows/ci-pull-request.yml))

Runs fast build and unit/contract checks on every PR push (including draft PRs) and is required by the **`Protected Main`** branch ruleset:

1. **Frontend Build & Unit Tests (`frontend-build-and-test`)**:
   - Checks code formatting with Prettier (`npm run format:check`).
   - Typechecks the k6 performance test suite against the Kiota-generated client models (`npm run typecheck:perf`).
   - Builds the Angular application in `production` configuration (`npm run build`), verifying bundle budgets.
   - Runs headless Vitest unit tests (`npm run test:ci`).
2. **Backend Build, Contract & Integration Tests (`backend-build-and-test`)**:
   - Compiles `main.slnx` in `Release` configuration while enforcing `.editorconfig` code style (`dotnet build main.slnx --configuration Release -p:EnforceCodeStyleInBuild=true`).
   - Verifies that [`docs/openapi/openapi.json`](docs/openapi/openapi.json) has no uncommitted drift after build.
   - Runs the MSTest backend integration test suite (`dotnet test --solution main.slnx --configuration Release --no-build`).

### 2. Pre-Merge CI ([`.github/workflows/ci-pre-merge.yml`](.github/workflows/ci-pre-merge.yml))

Runs heavier end-to-end system integration tests only when a PR is ready to merge (`ready_for_review` / `draft == false` or `merge_group`):

1. **System Integration Tests (`system-integration-tests`)**:
   - Starts the application stack in `Staging` mode (`aspire start -- --environment Staging`), provisioning a real PostgreSQL container (`LogisticsDatabase`) and waiting for `Backend` (`/health`) readiness (`aspire wait Backend --status up`).
   - Executes all 32 **Bruno v4** OpenCollection API conformance requests (`./tests/Logistics.Backend.APITests/run-api-tests.sh --base-url http://localhost:5045/v1 --reports`) against the containerized stack and stops the stack (`aspire stop`).

### Running Merge Request Checks Locally

Before opening or updating a Merge Request, run the same checks locally:

```bash
# Auto-format Frontend (Prettier) and Backend (.editorconfig)
npm run format
dotnet format main.slnx

# Run Frontend checks & unit tests
npm run format:check
npm run typecheck:perf
npm run build
npm run test:ci

# Run Backend build, OpenAPI drift check & MSTest integration tests
dotnet build main.slnx --configuration Release
git diff --exit-code docs/openapi/openapi.json
dotnet test --solution main.slnx --configuration Release --no-build

# Run Aspire Staging (PostgreSQL container) + Bruno API Conformance Suite
aspire start -- --environment Staging
aspire wait Backend --status up --timeout 120
./tests/Logistics.Backend.APITests/run-api-tests.sh --base-url http://localhost:5045/v1 --reports
aspire stop
```


