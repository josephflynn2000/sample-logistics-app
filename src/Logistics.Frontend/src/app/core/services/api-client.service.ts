import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FetchRequestAdapter, HttpClient } from '@microsoft/kiota-http-fetchlibrary';
import {
  AnonymousAuthenticationProvider,
  type SerializationWriterFactoryRegistry,
} from '@microsoft/kiota-abstractions';
import { JsonSerializationWriterFactory } from '@microsoft/kiota-serialization-json';
import {
  createApiClient,
  type ApiClient,
} from '../../../../../Logistics.ClientLibrary/apiClient';
import type {
  Driver,
  Vehicle,
  Route,
} from '../../../../../Logistics.ClientLibrary/models/index';

@Injectable({ providedIn: 'root' })
export class ApiClientService {
  private readonly platformId = inject(PLATFORM_ID);
  public readonly isBrowser = isPlatformBrowser(this.platformId);
  public readonly client: ApiClient;
  public readonly backendConnected = signal<boolean>(true);
  public readonly globalSearch = signal<string>('');

  private readonly etagCache = new Map<string, string>();

  constructor() {
    const authProvider = new AnonymousAuthenticationProvider();
    const httpClient = new HttpClient(async (url: string, init?: RequestInit) => {
      const requestInit: RequestInit = { ...init };
      const headers = new Headers(requestInit.headers);

      // Automatically attach If-Match ETag header on PUT / PATCH / DELETE requests
      const method = (requestInit.method ?? 'GET').toUpperCase();
      const requestResourceId = this.extractResourceId(url);
      if (
        (method === 'PUT' || method === 'PATCH' || method === 'DELETE') &&
        requestResourceId
      ) {
        const cachedEtag = this.etagCache.get(requestResourceId);
        if (cachedEtag && !headers.has('If-Match')) {
          headers.set('If-Match', cachedEtag);
        }
      }
      requestInit.headers = headers;

      const response = await fetch(url, requestInit);
      this.backendConnected.set(true);

      // Capture ETag header from response or Location header (for 201 Created)
      const etag = response.headers.get('ETag') ?? response.headers.get('etag');
      const locationHeader =
        response.headers.get('Location') ?? response.headers.get('location');
      const resolvedId =
        requestResourceId ??
        (locationHeader ? this.extractResourceId(locationHeader) : null);

      if (etag && resolvedId) {
        this.etagCache.set(resolvedId, etag);
      }

      return response;
    });

    const adapter = new FetchRequestAdapter(authProvider, undefined, undefined, httpClient);
    adapter.baseUrl =
      this.isBrowser && typeof window !== 'undefined'
        ? window.location.origin
        : 'http://localhost:5045';

    this.client = createApiClient(adapter);

    // Register JSON Merge Patch (RFC 7396) content-type serializer for Kiota PATCH endpoints
    const writerRegistry =
      adapter.getSerializationWriterFactory() as SerializationWriterFactoryRegistry;
    if (writerRegistry?.contentTypeAssociatedFactories) {
      const jsonFactory = new JsonSerializationWriterFactory();
      writerRegistry.contentTypeAssociatedFactories.set('application/merge-patch+json', {
        getValidContentType: () => 'application/merge-patch+json',
        getSerializationWriter: () => jsonFactory.getSerializationWriter('application/json'),
      });
    }
  }

  public setETag(resourceId: string, etag: string): void {
    this.etagCache.set(resourceId, etag);
  }

  public getETag(resourceId: string): string | undefined {
    return this.etagCache.get(resourceId);
  }

  public getStatusCode(error: unknown): number | null {
    if (!error || typeof error !== 'object') {
      return null;
    }
    const errRecord = error as Record<string, unknown>;
    if (typeof errRecord['responseStatusCode'] === 'number') {
      return errRecord['responseStatusCode'];
    }
    if (typeof errRecord['status'] === 'number') {
      return errRecord['status'];
    }
    return null;
  }

  public isPreconditionFailed(error: unknown): boolean {
    const code = this.getStatusCode(error);
    if (code === 412) {
      return true;
    }
    if (error && typeof error === 'object') {
      const msg = (error as Record<string, unknown>)['message'];
      return typeof msg === 'string' && msg.includes('412');
    }
    return false;
  }

  public isConflictError(error: unknown): boolean {
    return this.getStatusCode(error) === 409;
  }

  public isValidationError(error: unknown): boolean {
    return this.getStatusCode(error) === 400;
  }

  public getProblemDetail(error: unknown, fallback: string): string {
    if (error && typeof error === 'object') {
      const errRecord = error as Record<string, unknown>;
      if (typeof errRecord['detail'] === 'string' && errRecord['detail'].length > 0) {
        return errRecord['detail'];
      }
      if (typeof errRecord['title'] === 'string' && errRecord['title'].length > 0) {
        return errRecord['title'];
      }
    }
    return fallback;
  }

  private extractResourceId(url: string): string | null {
    const match = /\/v1\/(?:drivers|vehicles|routes)\/([0-9a-fA-F-]{36})/.exec(url);
    return match ? match[1] : null;
  }

  public getFallbackDrivers(): Driver[] {
    return [
      {
        id: 'd1042000-0000-4000-a000-000000000001',
        employeeNumber: 'EMP-1042',
        firstName: 'Marcus',
        lastName: 'Vance',
        email: 'mvance@logistics-fleet.com',
        phone: '+15552345678',
        licenseNumber: 'DL-88492019-IL',
        licenseClass: 'ClassA',
        licenseExpiryDate: '2028-04-12',
        status: 'Active',
      },
      {
        id: 'd0988000-0000-4000-a000-000000000002',
        employeeNumber: 'EMP-0988',
        firstName: 'Elena',
        lastName: 'Rostova',
        email: 'erostova@logistics-fleet.com',
        phone: '+15558765432',
        licenseNumber: 'DL-33109482-WI',
        licenseClass: 'ClassB',
        licenseExpiryDate: '2026-12-01',
        status: 'OnLeave',
      },
      {
        id: 'd1105000-0000-4000-a000-000000000003',
        employeeNumber: 'EMP-1105',
        firstName: 'Devon',
        lastName: 'Brooks',
        email: 'dbrooks@logistics-fleet.com',
        phone: '+15553928172',
        licenseNumber: 'DL-77412390-IN',
        licenseClass: 'ClassA',
        licenseExpiryDate: '2029-08-22',
        status: 'Active',
      },
      {
        id: 'd0814000-0000-4000-a000-000000000004',
        employeeNumber: 'EMP-0814',
        firstName: 'Carlos',
        lastName: 'Mendoza',
        email: 'cmendoza@logistics-fleet.com',
        phone: '+15556019982',
        licenseNumber: 'DL-55201938-MN',
        licenseClass: 'ClassC',
        licenseExpiryDate: '2027-03-19',
        status: 'Suspended',
      },
      {
        id: 'd1201000-0000-4000-a000-000000000005',
        employeeNumber: 'EMP-1201',
        firstName: 'Hannah',
        lastName: 'Lin',
        email: 'hlin@logistics-fleet.com',
        phone: '+15559283746',
        licenseNumber: 'DL-99201844-IL',
        licenseClass: 'ClassA',
        licenseExpiryDate: '2028-11-05',
        status: 'Active',
      },
    ];
  }

  public getFallbackVehicles(): Vehicle[] {
    return [
      {
        id: 'a1000000-0000-4000-a000-000000000001',
        vin: '1FUJGLDR8PL108921',
        licensePlate: 'IL • 892-TRK',
        make: 'Freightliner',
        model: 'Cascadia 126',
        year: 2024,
        type: 'SemiTractor',
        capacityKg: 18500,
        capacityVolumeM3: 85.0,
        status: 'Available',
      },
      {
        id: 'a1000000-0000-4000-a000-000000000002',
        vin: '4UZAA2AK4PC294410',
        licensePlate: 'IL • 441-BOX',
        make: 'Isuzu',
        model: 'N-Series NPR-HD',
        year: 2023,
        type: 'BoxTruck',
        capacityKg: 5200,
        capacityVolumeM3: 28.5,
        status: 'InMaintenance',
      },
      {
        id: 'a1000000-0000-4000-a000-000000000003',
        vin: '1FTBW2CM5NKA10823',
        licensePlate: 'IL • 108-VAN',
        make: 'Ford',
        model: 'Transit 350 HD',
        year: 2022,
        type: 'Van',
        capacityKg: 2100,
        capacityVolumeM3: 14.2,
        status: 'Available',
      },
      {
        id: 'a1000000-0000-4000-a000-000000000004',
        vin: '1XPBD49X1RD552901',
        licensePlate: 'WI • 552-CLD',
        make: 'Peterbilt',
        model: '579 Reefer',
        year: 2024,
        type: 'RefrigeratedTruck',
        capacityKg: 17000,
        capacityVolumeM3: 78.0,
        status: 'OutOfService',
      },
      {
        id: 'a1000000-0000-4000-a000-000000000005',
        vin: '1XKAD49X7PJ903112',
        licensePlate: 'MN • 903-FLT',
        make: 'Kenworth',
        model: 'T680 Flatbed',
        year: 2023,
        type: 'Flatbed',
        capacityKg: 20000,
        capacityVolumeM3: 65.0,
        status: 'Available',
      },
    ];
  }

  public getFallbackRoutes(): Route[] {
    return [
      {
        id: 'c1000000-0000-4000-a000-000000000001',
        code: 'RT-CHI-MSP-01',
        name: 'Chicago Depot to Minneapolis Hub',
        description: 'Primary I-90 / I-94 northern freight corridor with regional cross-dock stops.',
        status: 'Active',
        estimatedDistanceKm: 660.0,
        estimatedDurationMinutes: 465,
        waypoints: [
          {
            sequenceNumber: 1,
            label: 'Chicago South Intermodal Depot',
            address: '2800 S Ashland Ave, Chicago, IL',
            latitude: 41.8414,
            longitude: -87.6658,
            stopType: 'Origin',
            dwellTimeMinutes: 0,
          },
          {
            sequenceNumber: 2,
            label: 'Rockford Distribution Center',
            address: '6200 Material Ave, Rockford, IL',
            latitude: 42.2281,
            longitude: -89.0123,
            stopType: 'Pickup',
            dwellTimeMinutes: 45,
          },
          {
            sequenceNumber: 3,
            label: 'Madison Cold-Storage Crossroads',
            address: '4100 Hanson Rd, Madison, WI',
            latitude: 43.1325,
            longitude: -89.3082,
            stopType: 'Delivery',
            dwellTimeMinutes: 30,
          },
          {
            sequenceNumber: 4,
            label: 'Minneapolis North Terminal',
            address: '1900 Broadway St NE, Minneapolis, MN',
            latitude: 45.0031,
            longitude: -93.2492,
            stopType: 'Destination',
            dwellTimeMinutes: 0,
          },
        ],
      },
      {
        id: 'c1000000-0000-4000-a000-000000000002',
        code: 'RT-MIL-IND-04',
        name: 'Milwaukee Regional Express Delivery',
        description: 'Direct daily express run connecting Milwaukee port terminal to Indianapolis.',
        status: 'Draft',
        estimatedDistanceKm: 412.5,
        estimatedDurationMinutes: 270,
        waypoints: [
          {
            sequenceNumber: 1,
            label: 'Milwaukee Harbor Terminal',
            address: '1225 S Carferry Dr, Milwaukee, WI',
            latitude: 43.0121,
            longitude: -87.8952,
            stopType: 'Origin',
            dwellTimeMinutes: 15,
          },
          {
            sequenceNumber: 2,
            label: 'Indianapolis South Logistics Park',
            address: '5400 W Raymond St, Indianapolis, IN',
            latitude: 39.7361,
            longitude: -86.2519,
            stopType: 'Destination',
            dwellTimeMinutes: 20,
          },
        ],
      },
      {
        id: 'c1000000-0000-4000-a000-000000000003',
        code: 'RT-DET-STL-09',
        name: 'Great Lakes to St. Louis Supply Line',
        description: 'Automotive components and industrial freight route along I-69 and I-70.',
        status: 'Active',
        estimatedDistanceKm: 855.0,
        estimatedDurationMinutes: 540,
        waypoints: [
          {
            sequenceNumber: 1,
            label: 'Detroit Metro Assembly Hub',
            address: '9000 Michigan Ave, Dearborn, MI',
            latitude: 42.3122,
            longitude: -83.1763,
            stopType: 'Origin',
            dwellTimeMinutes: 0,
          },
          {
            sequenceNumber: 2,
            label: 'Fort Wayne Freight Annex',
            address: '3300 Coliseum Blvd, Fort Wayne, IN',
            latitude: 41.1165,
            longitude: -85.1582,
            stopType: 'Pickup',
            dwellTimeMinutes: 35,
          },
          {
            sequenceNumber: 3,
            label: 'St. Louis Gateway Warehouse',
            address: '1400 N Broadway, St. Louis, MO',
            latitude: 38.6412,
            longitude: -90.1848,
            stopType: 'Destination',
            dwellTimeMinutes: 0,
          },
        ],
      },
    ];
  }
}
