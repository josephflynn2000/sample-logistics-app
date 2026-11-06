export type ResourceKind = 'vehicles' | 'drivers' | 'routes';
export type OperationKind = 'read' | 'write' | 'health';
export type ScenarioName = 'smoke' | 'mixed' | 'stress' | 'spike' | 'endpoint' | 'vus';

export type OperationName =
  | 'Vehicles_Create'
  | 'Vehicles_List'
  | 'Vehicles_Search'
  | 'Vehicles_Get'
  | 'Vehicles_Replace'
  | 'Vehicles_Patch'
  | 'Vehicles_Delete'
  | 'Drivers_Create'
  | 'Drivers_List'
  | 'Drivers_Search'
  | 'Drivers_Get'
  | 'Drivers_Replace'
  | 'Drivers_Patch'
  | 'Drivers_Delete'
  | 'Routes_Create'
  | 'Routes_List'
  | 'Routes_Search'
  | 'Routes_Get'
  | 'Routes_Replace'
  | 'Routes_Patch'
  | 'Routes_GetWaypoints'
  | 'Routes_ReplaceWaypoints'
  | 'Routes_Delete';

export interface OperationMetadata {
  readonly resource: ResourceKind;
  readonly operation: OperationName;
  readonly method: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  readonly route: string;
  readonly kind: OperationKind;
}

const rawBaseUrl = (
  __ENV.BASE_URL ||
  __ENV.services__Backend__http__0 ||
  'http://localhost:5045'
).replace(/\/+$/, '');

export const API_BASE_URL = rawBaseUrl.endsWith('/v1') ? rawBaseUrl : `${rawBaseUrl}/v1`;

export const SCENARIO = (__ENV.SCENARIO || 'smoke').toLowerCase() as ScenarioName;
export const TARGET_OPERATION = (__ENV.OPERATION || 'Routes_List') as OperationName;
export const GENERATE_REPORTS = (__ENV.REPORTS || 'false').toLowerCase() === 'true';

export const DEFAULT_SEED_COUNT = Number(
  __ENV.SEED_COUNT || (SCENARIO === 'smoke' ? 10 : 100)
);

export const ALL_OPERATIONS: readonly OperationMetadata[] = [
  // Vehicles
  { resource: 'vehicles', operation: 'Vehicles_Create', method: 'POST', route: '/v1/vehicles', kind: 'write' },
  { resource: 'vehicles', operation: 'Vehicles_List', method: 'GET', route: '/v1/vehicles', kind: 'read' },
  { resource: 'vehicles', operation: 'Vehicles_Search', method: 'GET', route: '/v1/vehicles?search=...', kind: 'read' },
  { resource: 'vehicles', operation: 'Vehicles_Get', method: 'GET', route: '/v1/vehicles/{vehicleId}', kind: 'read' },
  { resource: 'vehicles', operation: 'Vehicles_Replace', method: 'PUT', route: '/v1/vehicles/{vehicleId}', kind: 'write' },
  { resource: 'vehicles', operation: 'Vehicles_Patch', method: 'PATCH', route: '/v1/vehicles/{vehicleId}', kind: 'write' },
  { resource: 'vehicles', operation: 'Vehicles_Delete', method: 'DELETE', route: '/v1/vehicles/{vehicleId}', kind: 'write' },
  // Drivers
  { resource: 'drivers', operation: 'Drivers_Create', method: 'POST', route: '/v1/drivers', kind: 'write' },
  { resource: 'drivers', operation: 'Drivers_List', method: 'GET', route: '/v1/drivers', kind: 'read' },
  { resource: 'drivers', operation: 'Drivers_Search', method: 'GET', route: '/v1/drivers?search=...', kind: 'read' },
  { resource: 'drivers', operation: 'Drivers_Get', method: 'GET', route: '/v1/drivers/{driverId}', kind: 'read' },
  { resource: 'drivers', operation: 'Drivers_Replace', method: 'PUT', route: '/v1/drivers/{driverId}', kind: 'write' },
  { resource: 'drivers', operation: 'Drivers_Patch', method: 'PATCH', route: '/v1/drivers/{driverId}', kind: 'write' },
  { resource: 'drivers', operation: 'Drivers_Delete', method: 'DELETE', route: '/v1/drivers/{driverId}', kind: 'write' },
  // Routes & Waypoints
  { resource: 'routes', operation: 'Routes_Create', method: 'POST', route: '/v1/routes', kind: 'write' },
  { resource: 'routes', operation: 'Routes_List', method: 'GET', route: '/v1/routes', kind: 'read' },
  { resource: 'routes', operation: 'Routes_Search', method: 'GET', route: '/v1/routes?search=...', kind: 'read' },
  { resource: 'routes', operation: 'Routes_Get', method: 'GET', route: '/v1/routes/{routeId}', kind: 'read' },
  { resource: 'routes', operation: 'Routes_Replace', method: 'PUT', route: '/v1/routes/{routeId}', kind: 'write' },
  { resource: 'routes', operation: 'Routes_Patch', method: 'PATCH', route: '/v1/routes/{routeId}', kind: 'write' },
  { resource: 'routes', operation: 'Routes_GetWaypoints', method: 'GET', route: '/v1/routes/{routeId}/waypoints', kind: 'read' },
  { resource: 'routes', operation: 'Routes_ReplaceWaypoints', method: 'PUT', route: '/v1/routes/{routeId}/waypoints', kind: 'write' },
  { resource: 'routes', operation: 'Routes_Delete', method: 'DELETE', route: '/v1/routes/{routeId}', kind: 'write' },
];

export function buildScenarioConfig(): Record<string, Record<string, unknown>> {
  const rate = Number(__ENV.RATE || 50);
  const duration = __ENV.DURATION || '30s';

  switch (SCENARIO) {
    case 'smoke':
      return {
        smoke: {
          executor: 'per-vu-iterations',
          vus: 1,
          iterations: Number(__ENV.ITERATIONS || 3),
          maxDuration: '2m',
        },
      };

    case 'mixed':
      return {
        mixed: {
          executor: 'constant-arrival-rate',
          rate,
          timeUnit: '1s',
          duration,
          preAllocatedVUs: Math.max(20, Math.ceil(rate / 2)),
          maxVUs: Math.max(100, rate * 3),
        },
      };

    case 'stress': {
      const maxRate = Number(__ENV.MAX_RATE || 1500);
      return {
        stress: {
          executor: 'ramping-arrival-rate',
          startRate: 50,
          timeUnit: '1s',
          preAllocatedVUs: 100,
          maxVUs: Number(__ENV.MAX_VUS || 1000),
          stages: [
            { target: Math.round(maxRate * 0.2), duration: '10s' },
            { target: Math.round(maxRate * 0.5), duration: '15s' },
            { target: Math.round(maxRate * 0.8), duration: '15s' },
            { target: maxRate, duration: '15s' },
            { target: 0, duration: '5s' },
          ],
        },
      };
    }

    case 'vus': {
      const maxVus = Number(__ENV.MAX_VUS || 500);
      return {
        vus: {
          executor: 'ramping-vus',
          startVUs: 10,
          stages: [
            { target: Math.round(maxVus * 0.2), duration: '10s' },
            { target: Math.round(maxVus * 0.5), duration: '15s' },
            { target: maxVus, duration: '15s' },
            { target: 0, duration: '5s' },
          ],
        },
      };
    }

    case 'spike':
      return {
        spike: {
          executor: 'ramping-arrival-rate',
          startRate: 20,
          timeUnit: '1s',
          preAllocatedVUs: 50,
          maxVUs: 400,
          stages: [
            { target: 20, duration: '10s' },
            { target: 250, duration: '15s' },
            { target: 20, duration: '15s' },
          ],
        },
      };

    case 'endpoint':
      return {
        endpoint: {
          executor: 'constant-arrival-rate',
          rate,
          timeUnit: '1s',
          duration,
          preAllocatedVUs: Math.max(20, Math.ceil(rate / 2)),
          maxVUs: Math.max(100, rate * 3),
        },
      };

    default:
      throw new Error(`Unknown SCENARIO '${SCENARIO}'. Expected smoke, mixed, stress, spike, or endpoint.`);
  }
}

export function buildThresholds(): Record<string, string[]> {
  const thresholds: Record<string, string[]> = {
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
    'http_req_duration{kind:read}': ['p(95)<150', 'p(99)<300'],
    'http_req_duration{kind:write}': ['p(95)<300', 'p(99)<600'],
    'http_req_duration{resource:vehicles}': ['p(95)<250'],
    'http_req_duration{resource:drivers}': ['p(95)<250'],
    'http_req_duration{resource:routes}': ['p(95)<250'],
  };

  for (const item of ALL_OPERATIONS) {
    const limit = item.kind === 'read' ? 300 : 500;
    thresholds[`http_req_duration{operation:${item.operation}}`] = [`p(95)<${limit}`];
  }

  return thresholds;
}
