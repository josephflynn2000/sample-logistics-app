import http from 'k6/http';
import exec from 'k6/execution';
import type {
  UpdateDriverRequest,
  UpdateRouteRequest,
  UpdateVehicleRequest,
} from '../../../src/Logistics.ClientLibrary/models/index.ts';
import {
  API_BASE_URL,
  DEFAULT_SEED_COUNT,
  SCENARIO,
  TARGET_OPERATION,
  buildScenarioConfig,
  buildThresholds,
  type OperationName,
} from './lib/config.ts';
import {
  SEARCH_TERMS,
  generateDriverPayload,
  generateRoutePayload,
  generateVehiclePayload,
  generateWaypoints,
} from './lib/generators.ts';
import * as client from './lib/client.ts';
import { buildSummaryOutputs, type K6SummaryData } from './lib/summary.ts';

export interface SeedContext {
  readonly runId: number;
  readonly vehicles: string[];
  readonly drivers: string[];
  readonly routes: string[];
}

export const options = {
  scenarios: buildScenarioConfig(),
  thresholds: buildThresholds(),
};

// ============================================================================
// Setup: Verify Backend Health & Seed Baseline Entities
// ============================================================================
export function setup(): SeedContext {
  const healthUrl = API_BASE_URL.replace(/\/v1$/, '/health');
  const healthRes = http.get(healthUrl, { tags: { name: 'Health_Check', kind: 'health' } });
  if (healthRes.status !== 200) {
    throw new Error(`Backend health check failed at ${healthUrl} (status=${healthRes.status})`);
  }

  const runId = Math.floor(Date.now() / 1000) % 100000;
  const seedCount = DEFAULT_SEED_COUNT;
  const vehicles: string[] = [];
  const drivers: string[] = [];
  const routes: string[] = [];

  for (let i = 0; i < seedCount; i++) {
    const v = client.createVehicle(generateVehiclePayload(runId, 0, i, 1));
    if (v.body?.id) {
      vehicles.push(v.body.id);
    }

    const d = client.createDriver(generateDriverPayload(runId, 0, i, 2));
    if (d.body?.id) {
      drivers.push(d.body.id);
    }

    const r = client.createRoute(generateRoutePayload(runId, 0, i, 3));
    if (r.body?.id) {
      routes.push(r.body.id);
    }
  }

  return {
    runId,
    vehicles,
    drivers,
    routes,
  };
}

// ============================================================================
// Scenario Execution
// ============================================================================
export default function (data: SeedContext): void {
  const vu = exec.vu.idInTest;
  const iter = exec.vu.iterationInScenario;

  if (SCENARIO === 'smoke') {
    runFullCrudSmokeCycle(data, vu, iter);
    return;
  }

  if (SCENARIO === 'endpoint') {
    runIsolatedEndpoint(data, vu, iter, TARGET_OPERATION);
    return;
  }

  runWeightedMixedOperation(data, vu, iter);
}

function pick<T>(list: readonly T[], idx: number): T {
  return list[idx % list.length];
}

// Exercises every single API across Vehicles, Drivers, and Routes
function runFullCrudSmokeCycle(data: SeedContext, vu: number, iter: number): void {
  // 1. Vehicles lifecycle
  const vPayload = generateVehiclePayload(data.runId, vu, iter, 10);
  const createdV = client.createVehicle(vPayload);
  const vList = client.listVehicles(10);
  if (vList.body?.nextPageToken) {
    client.listVehicles(10, vList.body.nextPageToken);
  }
  client.searchVehicles('Freightliner', 'Available');
  if (createdV.body?.id) {
    const vGet = client.getVehicle(createdV.body.id);
    const updatedVPayload: UpdateVehicleRequest = {
      ...vPayload,
      status: 'Available',
      capacityKg: (vPayload.capacityKg ?? 10000) + 500,
    };
    const vPut = client.replaceVehicle(createdV.body.id, updatedVPayload, vGet.etag);
    const vPatch = client.patchVehicle(createdV.body.id, { status: 'InMaintenance' }, vPut.etag);
    client.deleteVehicle(createdV.body.id, vPatch.etag);
  }

  // 2. Drivers lifecycle
  const dPayload = generateDriverPayload(data.runId, vu, iter, 20);
  const createdD = client.createDriver(dPayload);
  client.listDrivers(10);
  client.searchDrivers('Elena', 'Active');
  if (createdD.body?.id) {
    const dGet = client.getDriver(createdD.body.id);
    const updatedDPayload: UpdateDriverRequest = {
      ...dPayload,
      status: 'Active',
      licenseClass: 'ClassA',
    };
    const dPut = client.replaceDriver(createdD.body.id, updatedDPayload, dGet.etag);
    const dPatch = client.patchDriver(createdD.body.id, { status: 'OnLeave' }, dPut.etag);
    client.deleteDriver(createdD.body.id, dPatch.etag);
  }

  // 3. Routes & Waypoints lifecycle
  const rPayload = generateRoutePayload(data.runId, vu, iter, 30);
  const createdR = client.createRoute(rPayload);
  client.listRoutes(10);
  client.searchRoutes('Corridor', 'Active');
  if (createdR.body?.id) {
    const rGet = client.getRoute(createdR.body.id);
    const updatedRPayload: UpdateRouteRequest = {
      ...rPayload,
      status: 'Active',
      estimatedDistanceKm: 410.0,
    };
    const rPut = client.replaceRoute(createdR.body.id, updatedRPayload, rGet.etag);
    const rPatch = client.patchRoute(createdR.body.id, { status: 'Active' }, rPut.etag);
    client.getRouteWaypoints(createdR.body.id);
    const rWpPut = client.replaceRouteWaypoints(
      createdR.body.id,
      generateWaypoints(vu + iter + 1),
      rPatch.etag,
    );
    client.deleteRoute(createdR.body.id, rWpPut.etag);
  }
}

// Weighted realistic traffic distribution (80% reads / 20% writes) across all 3 resources
function runWeightedMixedOperation(data: SeedContext, vu: number, iter: number): void {
  const bucket = (vu * 37 + iter) % 100;
  const resourceIdx = (vu + iter) % 3; // 0: vehicles, 1: drivers, 2: routes

  // 0..34 (35%): GET /{id}
  if (bucket < 35) {
    if (resourceIdx === 0) client.getVehicle(pick(data.vehicles, vu + iter));
    else if (resourceIdx === 1) client.getDriver(pick(data.drivers, vu + iter));
    else client.getRoute(pick(data.routes, vu + iter));
    return;
  }

  // 35..59 (25%): GET / list (with pageToken follow-up on half of calls)
  if (bucket < 60) {
    if (resourceIdx === 0) {
      const res = client.listVehicles(25);
      const token = res.body?.nextPageToken ?? null;
      if (token && iter % 2 === 0) client.listVehicles(25, token);
    } else if (resourceIdx === 1) {
      const res = client.listDrivers(25);
      const token = res.body?.nextPageToken ?? null;
      if (token && iter % 2 === 0) client.listDrivers(25, token);
    } else {
      const res = client.listRoutes(25);
      const token = res.body?.nextPageToken ?? null;
      if (token && iter % 2 === 0) client.listRoutes(25, token);
    }
    return;
  }

  // 60..74 (15%): GET /?search=...
  if (bucket < 75) {
    if (resourceIdx === 0) client.searchVehicles(pick(SEARCH_TERMS.vehicles, vu + iter));
    else if (resourceIdx === 1) client.searchDrivers(pick(SEARCH_TERMS.drivers, vu + iter));
    else client.searchRoutes(pick(SEARCH_TERMS.routes, vu + iter));
    return;
  }

  // 75..79 (5%): GET /routes/{id}/waypoints
  if (bucket < 80) {
    client.getRouteWaypoints(pick(data.routes, vu + iter));
    return;
  }

  // 80..87 (8%): POST create
  if (bucket < 88) {
    if (resourceIdx === 0) client.createVehicle(generateVehiclePayload(data.runId, vu, iter, 40));
    else if (resourceIdx === 1)
      client.createDriver(generateDriverPayload(data.runId, vu, iter, 41));
    else client.createRoute(generateRoutePayload(data.runId, vu, iter, 42));
    return;
  }

  // 88..93 (6%): PATCH partial update
  if (bucket < 94) {
    if (resourceIdx === 0) {
      client.patchVehicle(pick(data.vehicles, vu + iter), {
        status: iter % 2 === 0 ? 'Available' : 'InMaintenance',
      });
    } else if (resourceIdx === 1) {
      client.patchDriver(pick(data.drivers, vu + iter), {
        status: iter % 2 === 0 ? 'Active' : 'OnLeave',
      });
    } else {
      client.patchRoute(pick(data.routes, vu + iter), {
        status: iter % 2 === 0 ? 'Active' : 'Draft',
      });
    }
    return;
  }

  // 94..97 (4%): PUT full replace / replace waypoints
  if (bucket < 98) {
    if (resourceIdx === 0) {
      const id = pick(data.vehicles, vu + iter);
      const current = client.getVehicle(id);
      if (current.body) {
        client.replaceVehicle(
          id,
          { ...current.body, capacityKg: (current.body.capacityKg ?? 10000) + 10 },
          current.etag,
        );
      }
    } else if (resourceIdx === 1) {
      const id = pick(data.drivers, vu + iter);
      const current = client.getDriver(id);
      if (current.body) {
        client.replaceDriver(id, current.body, current.etag);
      }
    } else {
      const id = pick(data.routes, vu + iter);
      if (iter % 2 === 0) {
        client.replaceRouteWaypoints(id, generateWaypoints(vu + iter));
      } else {
        const current = client.getRoute(id);
        if (current.body) {
          client.replaceRoute(id, current.body, current.etag);
        }
      }
    }
    return;
  }

  // 98..99 (2%): Ephemeral create + DELETE
  if (resourceIdx === 0) {
    const created = client.createVehicle(generateVehiclePayload(data.runId, vu, iter, 90));
    if (created.body?.id) client.deleteVehicle(created.body.id, created.etag);
  } else if (resourceIdx === 1) {
    const created = client.createDriver(generateDriverPayload(data.runId, vu, iter, 91));
    if (created.body?.id) client.deleteDriver(created.body.id, created.etag);
  } else {
    const created = client.createRoute(generateRoutePayload(data.runId, vu, iter, 92));
    if (created.body?.id) client.deleteRoute(created.body.id, created.etag);
  }
}

function runIsolatedEndpoint(
  data: SeedContext,
  vu: number,
  iter: number,
  operation: OperationName,
): void {
  switch (operation) {
    case 'Vehicles_List':
      client.listVehicles(25);
      break;
    case 'Vehicles_Search':
      client.searchVehicles(pick(SEARCH_TERMS.vehicles, vu + iter));
      break;
    case 'Vehicles_Get':
      client.getVehicle(pick(data.vehicles, vu + iter));
      break;
    case 'Vehicles_Create':
      client.createVehicle(generateVehiclePayload(data.runId, vu, iter, 50));
      break;
    case 'Drivers_List':
      client.listDrivers(25);
      break;
    case 'Drivers_Search':
      client.searchDrivers(pick(SEARCH_TERMS.drivers, vu + iter));
      break;
    case 'Drivers_Get':
      client.getDriver(pick(data.drivers, vu + iter));
      break;
    case 'Drivers_Create':
      client.createDriver(generateDriverPayload(data.runId, vu, iter, 60));
      break;
    case 'Routes_List':
      client.listRoutes(25);
      break;
    case 'Routes_Search':
      client.searchRoutes(pick(SEARCH_TERMS.routes, vu + iter));
      break;
    case 'Routes_Get':
      client.getRoute(pick(data.routes, vu + iter));
      break;
    case 'Routes_GetWaypoints':
      client.getRouteWaypoints(pick(data.routes, vu + iter));
      break;
    case 'Routes_Create':
      client.createRoute(generateRoutePayload(data.runId, vu, iter, 70));
      break;
    default:
      throw new Error(`Unsupported OPERATION '${operation}' for endpoint scenario.`);
  }
}

export function handleSummary(data: K6SummaryData): Record<string, string> {
  return buildSummaryOutputs(data);
}
