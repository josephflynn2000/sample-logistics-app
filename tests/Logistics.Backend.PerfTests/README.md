# Logistics.Backend Performance & Stress Test Suite (k6 + TypeScript)

Standalone, CI/CD-ready [Grafana k6](https://k6.io/) performance and stress test suite for `Logistics.Backend` (`/v1/vehicles`, `/v1/drivers`, `/v1/routes`, and `/v1/routes/{routeId}/waypoints`).

Written in strict TypeScript and typed directly against the Kiota-generated OpenAPI models in [`src/Logistics.ClientLibrary/models/index.ts`](../../src/Logistics.ClientLibrary/models/index.ts). Runs natively via `k6 run` with no bundler or custom bash script required.

## Prerequisites

- [Grafana k6 v0.52+](https://grafana.com/docs/k6/latest/set-up/install-k6/) (`brew install k6` on macOS)
- Running instance of `Logistics.Backend` (via `aspire start -- --environment Staging`, `aspire start`, or `dotnet run --project src/Logistics.Backend`)

## Type Checking Against Kiota Client Models

```bash
npx tsc --noEmit -p tests/Logistics.Backend.PerfTests/tsconfig.json
```

## Quick Start

### 1. Smoke Test (End-to-End CRUD + Pagination + Search + ETag Concurrency)

```bash
k6 run -e BASE_URL=http://localhost:5045 -e SCENARIO=smoke tests/Logistics.Backend.PerfTests/k6/main.ts
```

### 2. Mixed Arrival-Rate Workload (80% Reads / 20% Writes)

Seeds a baseline dataset (`SEED_COUNT`, default `100` per resource) during `setup()` and runs a constant arrival-rate mix across Vehicles, Drivers, Routes, and Waypoints:

```bash
k6 run \
  -e BASE_URL=http://localhost:5045 \
  -e SCENARIO=mixed \
  -e RATE=100 \
  -e DURATION=1m \
  -e SEED_COUNT=200 \
  -e REPORTS=true \
  tests/Logistics.Backend.PerfTests/k6/main.ts
```

### 3. Stress Test (Ramping Arrival Rate)

Steps arrival rate from `20 -> 50 -> 120 -> 250 RPS` to identify latency degradation or database connection saturation:

```bash
k6 run -e BASE_URL=http://localhost:5045 -e SCENARIO=stress tests/Logistics.Backend.PerfTests/k6/main.ts
```

### 4. Spike Test (Sudden Traffic Burst)

```bash
k6 run -e BASE_URL=http://localhost:5045 -e SCENARIO=spike tests/Logistics.Backend.PerfTests/k6/main.ts
```

### 5. Single-Endpoint Isolation (for Profiling)

Isolates a single operation (`Vehicles_List`, `Vehicles_Search`, `Drivers_List`, `Drivers_Search`, `Routes_List`, `Routes_Search`, `Routes_Get`, `Routes_GetWaypoints`, etc.) at a constant arrival rate:

```bash
k6 run \
  -e BASE_URL=http://localhost:5045 \
  -e SCENARIO=endpoint \
  -e OPERATION=Routes_Search \
  -e RATE=100 \
  -e DURATION=30s \
  tests/Logistics.Backend.PerfTests/k6/main.ts
```

### 6. Built-In Web Dashboard & HTML Report Export

Use k6's built-in web dashboard environment variables alongside `REPORTS=true` to generate JUnit XML (`reports/junit.xml`), JSON (`reports/summary.json`), and an interactive HTML report (`reports/dashboard.html`):

```bash
K6_WEB_DASHBOARD=true \
K6_WEB_DASHBOARD_EXPORT=tests/Logistics.Backend.PerfTests/reports/dashboard.html \
k6 run -e BASE_URL=http://localhost:5045 -e SCENARIO=mixed -e REPORTS=true tests/Logistics.Backend.PerfTests/k6/main.ts
```

## Environment Variables Reference

| Variable | Default | Description |
| :--- | :--- | :--- |
| `BASE_URL` | `http://localhost:5045` | Base URL of `Logistics.Backend` |
| `SCENARIO` | `smoke` | `smoke`, `mixed`, `stress`, `spike`, or `endpoint` |
| `OPERATION` | `Routes_List` | Operation ID when `SCENARIO=endpoint` |
| `RATE` | `50` | Target arrival rate (`iters/s`) for `mixed` and `endpoint` |
| `DURATION` | `30s` | Test duration for `mixed` and `endpoint` |
| `SEED_COUNT` | `10` (`smoke`) / `100` (others) | Number of Vehicles, Drivers, and Routes seeded in `setup()` |
| `REPORTS` | `false` | When `true`, writes `reports/summary.json` and `reports/junit.xml` |
