import type {
  CreateDriverRequest,
  CreateRouteRequest,
  CreateVehicleRequest,
  DriverStatus,
  LicenseClass,
  RouteStatus,
  RouteWaypoint,
  StopType,
  VehicleStatus,
  VehicleType,
} from '../../../../src/Logistics.ClientLibrary/models/index.ts';

// Valid VIN alphabet (17 chars, excludes I, O, Q per ^[A-HJ-NPR-Z0-9]{17}$)
const VIN_ALPHABET = '0123456789ABCDEFGHJKLMNPRSTUVWXYZ';

interface VehicleSpec {
  readonly make: string;
  readonly model: string;
  readonly type: VehicleType;
  readonly kg: number;
  readonly vol: number;
}

const VEHICLE_MAKES: readonly VehicleSpec[] = [
  { make: 'Freightliner', model: 'Cascadia 126', type: 'SemiTractor', kg: 18500, vol: 85.0 },
  { make: 'Volvo', model: 'VNL 860', type: 'SemiTractor', kg: 19000, vol: 88.0 },
  { make: 'Isuzu', model: 'NPR-HD', type: 'BoxTruck', kg: 5200, vol: 28.5 },
  { make: 'Ford', model: 'Transit 350 HD', type: 'Van', kg: 2100, vol: 14.2 },
  { make: 'Kenworth', model: 'T680 Reefer', type: 'RefrigeratedTruck', kg: 16800, vol: 72.0 },
  { make: 'Peterbilt', model: '579 Flatbed', type: 'Flatbed', kg: 20000, vol: 90.0 },
];

const VEHICLE_STATUSES: readonly VehicleStatus[] = ['Available', 'InMaintenance', 'OutOfService'];
const DRIVER_FIRST_NAMES: readonly string[] = ['Elena', 'Marcus', 'Devon', 'Sofia', 'Liam', 'Nadia', 'Carlos', 'Hannah'];
const DRIVER_LAST_NAMES: readonly string[] = ['Rostova', 'Vance', 'Brooks', 'Patel', 'Nguyen', 'Kowalski', 'Silva', 'Chen'];
const LICENSE_CLASSES: readonly LicenseClass[] = ['ClassA', 'ClassB', 'ClassC', 'Standard'];
const DRIVER_STATUSES: readonly DriverStatus[] = ['Active', 'Inactive', 'OnLeave'];
const ROUTE_STATUSES: readonly RouteStatus[] = ['Draft', 'Active', 'Archived'];

interface CityWaypointSpec {
  readonly label: string;
  readonly address: string;
  readonly lat: number;
  readonly lon: number;
}

const CITIES: readonly CityWaypointSpec[] = [
  { label: 'Chicago Hub', address: '233 S Wacker Dr, Chicago, IL', lat: 41.8781, lon: -87.6298 },
  { label: 'Joliet Intermodal', address: '3000 Channahon Rd, Joliet, IL', lat: 41.4686, lon: -88.1517 },
  { label: 'Indianapolis Depot', address: '500 W Washington St, Indianapolis, IN', lat: 39.7684, lon: -86.1581 },
  { label: 'Milwaukee Terminal', address: '400 E Wisconsin Ave, Milwaukee, WI', lat: 43.0389, lon: -87.9065 },
  { label: 'St. Louis Gateway', address: '1 Market St, St. Louis, MO', lat: 38.627, lon: -90.1994 },
  { label: 'Detroit Logistics Park', address: '100 Renaissance Ctr, Detroit, MI', lat: 42.3314, lon: -83.0458 },
];

export const SEARCH_TERMS: Readonly<Record<'vehicles' | 'drivers' | 'routes', readonly string[]>> = {
  vehicles: ['freight', 'volvo', 'isuzu', 'ford', 'cascadia', 'transit'],
  drivers: ['elena', 'marcus', 'devon', 'sofia', 'emp-', 'example.com'],
  routes: ['chi', 'ind', 'mke', 'stl', 'corridor', 'rt-'],
};

function encodeBase33(num: number, length: number): string {
  let value = Math.abs(Math.floor(num));
  let out = '';
  for (let i = 0; i < length; i++) {
    out = VIN_ALPHABET[value % VIN_ALPHABET.length] + out;
    value = Math.floor(value / VIN_ALPHABET.length);
  }
  return out;
}

export function uniqueToken(runId: number, vu: number, iter: number, salt = 0): string {
  const composite = runId * 10000000 + vu * 100000 + iter * 10 + salt;
  return encodeBase33(composite, 9);
}

export function generateVehiclePayload(
  runId: number,
  vu: number,
  iter: number,
  salt = 0
): CreateVehicleRequest {
  const token = uniqueToken(runId, vu, iter, salt);
  const spec = VEHICLE_MAKES[(vu + iter + salt) % VEHICLE_MAKES.length];
  const vinPrefix = encodeBase33(runId + salt * 17, 8);
  const vin = `${vinPrefix}${token}`; // Exactly 17 valid VIN chars

  return {
    vin,
    licensePlate: `PF-${token}`,
    make: spec.make,
    model: spec.model,
    year: 2020 + ((vu + iter) % 6),
    type: spec.type,
    capacityKg: spec.kg,
    capacityVolumeM3: spec.vol,
    status: VEHICLE_STATUSES[(vu + iter) % VEHICLE_STATUSES.length],
  };
}

export function generateDriverPayload(
  runId: number,
  vu: number,
  iter: number,
  salt = 0
): CreateDriverRequest {
  const token = uniqueToken(runId, vu, iter, salt);
  const first = DRIVER_FIRST_NAMES[(vu + iter) % DRIVER_FIRST_NAMES.length];
  const last = DRIVER_LAST_NAMES[(vu + iter * 3 + salt) % DRIVER_LAST_NAMES.length];
  const phoneDigits = String(1000000000 + ((runId * 100000 + vu * 1000 + iter + salt) % 8999999999));

  return {
    employeeNumber: `EMP-${token}`,
    firstName: first,
    lastName: last,
    email: `${first.toLowerCase()}.${token.toLowerCase()}@example.com`,
    phone: `+1${phoneDigits}`,
    licenseNumber: `DL-${token}`,
    licenseClass: LICENSE_CLASSES[(vu + iter) % LICENSE_CLASSES.length],
    licenseExpiryDate: '2028-12-31',
    status: DRIVER_STATUSES[(vu + iter) % DRIVER_STATUSES.length],
  };
}

export function generateWaypoints(seedIndex: number): RouteWaypoint[] {
  const origin = CITIES[seedIndex % CITIES.length];
  const pickup = CITIES[(seedIndex + 1) % CITIES.length];
  const delivery = CITIES[(seedIndex + 2) % CITIES.length];
  const destination = CITIES[(seedIndex + 3) % CITIES.length];

  const stopOrigin: StopType = 'Origin';
  const stopPickup: StopType = 'Pickup';
  const stopDelivery: StopType = 'Delivery';
  const stopDestination: StopType = 'Destination';

  return [
    {
      sequenceNumber: 1,
      label: origin.label,
      address: origin.address,
      latitude: origin.lat,
      longitude: origin.lon,
      stopType: stopOrigin,
      dwellTimeMinutes: 15,
    },
    {
      sequenceNumber: 2,
      label: pickup.label,
      address: pickup.address,
      latitude: pickup.lat,
      longitude: pickup.lon,
      stopType: stopPickup,
      dwellTimeMinutes: 30,
    },
    {
      sequenceNumber: 3,
      label: delivery.label,
      address: delivery.address,
      latitude: delivery.lat,
      longitude: delivery.lon,
      stopType: stopDelivery,
      dwellTimeMinutes: 25,
    },
    {
      sequenceNumber: 4,
      label: destination.label,
      address: destination.address,
      latitude: destination.lat,
      longitude: destination.lon,
      stopType: stopDestination,
      dwellTimeMinutes: 10,
    },
  ];
}

export function generateRoutePayload(
  runId: number,
  vu: number,
  iter: number,
  salt = 0
): CreateRouteRequest {
  const token = uniqueToken(runId, vu, iter, salt);
  const idx = vu + iter + salt;
  const origin = CITIES[idx % CITIES.length];
  const dest = CITIES[(idx + 3) % CITIES.length];

  return {
    code: `RT-${token}`,
    name: `${origin.label} to ${dest.label} Corridor (${token})`,
    description: `Regional freight transit corridor between ${origin.label} and ${dest.label}`,
    status: ROUTE_STATUSES[idx % ROUTE_STATUSES.length],
    estimatedDistanceKm: 285.5 + (idx % 200),
    estimatedDurationMinutes: 210 + (idx % 120),
    waypoints: generateWaypoints(idx),
  };
}
