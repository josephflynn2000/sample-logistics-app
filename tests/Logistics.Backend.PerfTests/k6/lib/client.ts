import http, { type Params, type Response } from 'k6/http';
import { check } from 'k6';
import type {
  CreateDriverRequest,
  CreateRouteRequest,
  CreateVehicleRequest,
  Driver,
  DriverListResponse,
  DriverStatus,
  PatchDriverRequest,
  PatchRouteRequest,
  PatchVehicleRequest,
  ReplaceWaypointsRequest,
  Route,
  RouteListResponse,
  RouteStatus,
  RouteWaypoint,
  RouteWaypointsResponse,
  UpdateDriverRequest,
  UpdateRouteRequest,
  UpdateVehicleRequest,
  Vehicle,
  VehicleListResponse,
  VehicleStatus,
} from '../../../../src/Logistics.ClientLibrary/models/index.ts';
import {
  API_BASE_URL,
  type OperationKind,
  type OperationName,
  type ResourceKind,
} from './config.ts';

export interface EntityResult<T> {
  readonly res: Response;
  readonly body: T | null;
  readonly etag: string;
}

const JSON_HEADERS: Record<string, string> = {
  'Content-Type': 'application/json',
  Accept: 'application/json',
};

function params(
  resource: ResourceKind,
  operation: OperationName,
  kind: OperationKind,
  extraHeaders?: Record<string, string>
): Params {
  return {
    headers: extraHeaders ? Object.assign({}, JSON_HEADERS, extraHeaders) : JSON_HEADERS,
    tags: {
      name: operation,
      operation,
      resource,
      kind,
    },
  };
}

export function getETag(res: Response): string {
  return res.headers['ETag'] || res.headers['Etag'] || res.headers['etag'] || '*';
}

// ============================================================================
// Vehicles API (/v1/vehicles)
// ============================================================================

export function createVehicle(payload: CreateVehicleRequest): EntityResult<Vehicle> {
  const res = http.post(
    `${API_BASE_URL}/vehicles`,
    JSON.stringify(payload),
    params('vehicles', 'Vehicles_Create', 'write')
  );
  check(res, {
    'Vehicles_Create is 201': (r) => r.status === 201,
  });
  return {
    res,
    body: res.status === 201 ? res.json<Vehicle>() : null,
    etag: getETag(res),
  };
}

export function listVehicles(
  pageSize = 25,
  pageToken: string | null = null,
  orderBy = 'createdAt desc'
): EntityResult<VehicleListResponse> {
  let url = `${API_BASE_URL}/vehicles?pageSize=${pageSize}&orderBy=${encodeURIComponent(orderBy)}`;
  if (pageToken) {
    url += `&pageToken=${encodeURIComponent(pageToken)}`;
  }
  const res = http.get(url, params('vehicles', 'Vehicles_List', 'read'));
  check(res, {
    'Vehicles_List is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<VehicleListResponse>() : null,
    etag: getETag(res),
  };
}

export function searchVehicles(
  searchTerm: string,
  status: VehicleStatus | null = null
): EntityResult<VehicleListResponse> {
  let url = `${API_BASE_URL}/vehicles?pageSize=25&search=${encodeURIComponent(searchTerm)}`;
  if (status) {
    url += `&status=${encodeURIComponent(status)}`;
  }
  const res = http.get(url, params('vehicles', 'Vehicles_Search', 'read'));
  check(res, {
    'Vehicles_Search is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<VehicleListResponse>() : null,
    etag: getETag(res),
  };
}

export function getVehicle(vehicleId: string): EntityResult<Vehicle> {
  const res = http.get(
    `${API_BASE_URL}/vehicles/${vehicleId}`,
    params('vehicles', 'Vehicles_Get', 'read')
  );
  check(res, {
    'Vehicles_Get is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<Vehicle>() : null,
    etag: getETag(res),
  };
}

export function replaceVehicle(
  vehicleId: string,
  payload: UpdateVehicleRequest,
  ifMatch = '*'
): EntityResult<Vehicle> {
  const res = http.put(
    `${API_BASE_URL}/vehicles/${vehicleId}`,
    JSON.stringify(payload),
    params('vehicles', 'Vehicles_Replace', 'write', { 'If-Match': ifMatch })
  );
  check(res, {
    'Vehicles_Replace is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<Vehicle>() : null,
    etag: getETag(res),
  };
}

export function patchVehicle(
  vehicleId: string,
  patchPayload: PatchVehicleRequest,
  ifMatch = '*'
): EntityResult<Vehicle> {
  const res = http.patch(
    `${API_BASE_URL}/vehicles/${vehicleId}`,
    JSON.stringify(patchPayload),
    params('vehicles', 'Vehicles_Patch', 'write', {
      'Content-Type': 'application/merge-patch+json',
      'If-Match': ifMatch,
    })
  );
  check(res, {
    'Vehicles_Patch is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<Vehicle>() : null,
    etag: getETag(res),
  };
}

export function deleteVehicle(vehicleId: string, ifMatch = '*'): Response {
  const res = http.del(
    `${API_BASE_URL}/vehicles/${vehicleId}`,
    null,
    params('vehicles', 'Vehicles_Delete', 'write', { 'If-Match': ifMatch })
  );
  check(res, {
    'Vehicles_Delete is 204': (r) => r.status === 204,
  });
  return res;
}

// ============================================================================
// Drivers API (/v1/drivers)
// ============================================================================

export function createDriver(payload: CreateDriverRequest): EntityResult<Driver> {
  const res = http.post(
    `${API_BASE_URL}/drivers`,
    JSON.stringify(payload),
    params('drivers', 'Drivers_Create', 'write')
  );
  check(res, {
    'Drivers_Create is 201': (r) => r.status === 201,
  });
  return {
    res,
    body: res.status === 201 ? res.json<Driver>() : null,
    etag: getETag(res),
  };
}

export function listDrivers(
  pageSize = 25,
  pageToken: string | null = null,
  orderBy = 'createdAt desc'
): EntityResult<DriverListResponse> {
  let url = `${API_BASE_URL}/drivers?pageSize=${pageSize}&orderBy=${encodeURIComponent(orderBy)}`;
  if (pageToken) {
    url += `&pageToken=${encodeURIComponent(pageToken)}`;
  }
  const res = http.get(url, params('drivers', 'Drivers_List', 'read'));
  check(res, {
    'Drivers_List is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<DriverListResponse>() : null,
    etag: getETag(res),
  };
}

export function searchDrivers(
  searchTerm: string,
  status: DriverStatus | null = null
): EntityResult<DriverListResponse> {
  let url = `${API_BASE_URL}/drivers?pageSize=25&search=${encodeURIComponent(searchTerm)}`;
  if (status) {
    url += `&status=${encodeURIComponent(status)}`;
  }
  const res = http.get(url, params('drivers', 'Drivers_Search', 'read'));
  check(res, {
    'Drivers_Search is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<DriverListResponse>() : null,
    etag: getETag(res),
  };
}

export function getDriver(driverId: string): EntityResult<Driver> {
  const res = http.get(
    `${API_BASE_URL}/drivers/${driverId}`,
    params('drivers', 'Drivers_Get', 'read')
  );
  check(res, {
    'Drivers_Get is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<Driver>() : null,
    etag: getETag(res),
  };
}

export function replaceDriver(
  driverId: string,
  payload: UpdateDriverRequest,
  ifMatch = '*'
): EntityResult<Driver> {
  const res = http.put(
    `${API_BASE_URL}/drivers/${driverId}`,
    JSON.stringify(payload),
    params('drivers', 'Drivers_Replace', 'write', { 'If-Match': ifMatch })
  );
  check(res, {
    'Drivers_Replace is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<Driver>() : null,
    etag: getETag(res),
  };
}

export function patchDriver(
  driverId: string,
  patchPayload: PatchDriverRequest,
  ifMatch = '*'
): EntityResult<Driver> {
  const res = http.patch(
    `${API_BASE_URL}/drivers/${driverId}`,
    JSON.stringify(patchPayload),
    params('drivers', 'Drivers_Patch', 'write', {
      'Content-Type': 'application/merge-patch+json',
      'If-Match': ifMatch,
    })
  );
  check(res, {
    'Drivers_Patch is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<Driver>() : null,
    etag: getETag(res),
  };
}

export function deleteDriver(driverId: string, ifMatch = '*'): Response {
  const res = http.del(
    `${API_BASE_URL}/drivers/${driverId}`,
    null,
    params('drivers', 'Drivers_Delete', 'write', { 'If-Match': ifMatch })
  );
  check(res, {
    'Drivers_Delete is 204': (r) => r.status === 204,
  });
  return res;
}

// ============================================================================
// Routes & Waypoints API (/v1/routes)
// ============================================================================

export function createRoute(payload: CreateRouteRequest): EntityResult<Route> {
  const res = http.post(
    `${API_BASE_URL}/routes`,
    JSON.stringify(payload),
    params('routes', 'Routes_Create', 'write')
  );
  check(res, {
    'Routes_Create is 201': (r) => r.status === 201,
  });
  return {
    res,
    body: res.status === 201 ? res.json<Route>() : null,
    etag: getETag(res),
  };
}

export function listRoutes(
  pageSize = 25,
  pageToken: string | null = null,
  orderBy = 'createdAt desc'
): EntityResult<RouteListResponse> {
  let url = `${API_BASE_URL}/routes?pageSize=${pageSize}&orderBy=${encodeURIComponent(orderBy)}`;
  if (pageToken) {
    url += `&pageToken=${encodeURIComponent(pageToken)}`;
  }
  const res = http.get(url, params('routes', 'Routes_List', 'read'));
  check(res, {
    'Routes_List is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<RouteListResponse>() : null,
    etag: getETag(res),
  };
}

export function searchRoutes(
  searchTerm: string,
  status: RouteStatus | null = null
): EntityResult<RouteListResponse> {
  let url = `${API_BASE_URL}/routes?pageSize=25&search=${encodeURIComponent(searchTerm)}`;
  if (status) {
    url += `&status=${encodeURIComponent(status)}`;
  }
  const res = http.get(url, params('routes', 'Routes_Search', 'read'));
  check(res, {
    'Routes_Search is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<RouteListResponse>() : null,
    etag: getETag(res),
  };
}

export function getRoute(routeId: string): EntityResult<Route> {
  const res = http.get(
    `${API_BASE_URL}/routes/${routeId}`,
    params('routes', 'Routes_Get', 'read')
  );
  check(res, {
    'Routes_Get is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<Route>() : null,
    etag: getETag(res),
  };
}

export function replaceRoute(
  routeId: string,
  payload: UpdateRouteRequest,
  ifMatch = '*'
): EntityResult<Route> {
  const res = http.put(
    `${API_BASE_URL}/routes/${routeId}`,
    JSON.stringify(payload),
    params('routes', 'Routes_Replace', 'write', { 'If-Match': ifMatch })
  );
  check(res, {
    'Routes_Replace is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<Route>() : null,
    etag: getETag(res),
  };
}

export function patchRoute(
  routeId: string,
  patchPayload: PatchRouteRequest,
  ifMatch = '*'
): EntityResult<Route> {
  const res = http.patch(
    `${API_BASE_URL}/routes/${routeId}`,
    JSON.stringify(patchPayload),
    params('routes', 'Routes_Patch', 'write', {
      'Content-Type': 'application/merge-patch+json',
      'If-Match': ifMatch,
    })
  );
  check(res, {
    'Routes_Patch is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<Route>() : null,
    etag: getETag(res),
  };
}

export function getRouteWaypoints(routeId: string): EntityResult<RouteWaypointsResponse> {
  const res = http.get(
    `${API_BASE_URL}/routes/${routeId}/waypoints`,
    params('routes', 'Routes_GetWaypoints', 'read')
  );
  check(res, {
    'Routes_GetWaypoints is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<RouteWaypointsResponse>() : null,
    etag: getETag(res),
  };
}

export function replaceRouteWaypoints(
  routeId: string,
  waypoints: RouteWaypoint[],
  ifMatch = '*'
): EntityResult<RouteWaypointsResponse> {
  const body: ReplaceWaypointsRequest = { waypoints };
  const res = http.put(
    `${API_BASE_URL}/routes/${routeId}/waypoints`,
    JSON.stringify(body),
    params('routes', 'Routes_ReplaceWaypoints', 'write', { 'If-Match': ifMatch })
  );
  check(res, {
    'Routes_ReplaceWaypoints is 200': (r) => r.status === 200,
  });
  return {
    res,
    body: res.status === 200 ? res.json<RouteWaypointsResponse>() : null,
    etag: getETag(res),
  };
}

export function deleteRoute(routeId: string, ifMatch = '*'): Response {
  const res = http.del(
    `${API_BASE_URL}/routes/${routeId}`,
    null,
    params('routes', 'Routes_Delete', 'write', { 'If-Match': ifMatch })
  );
  check(res, {
    'Routes_Delete is 204': (r) => r.status === 204,
  });
  return res;
}
