import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';
import { DriversList } from './features/drivers/drivers-list';
import { VehiclesList } from './features/vehicles/vehicles-list';
import { RoutesList } from './features/routes/routes-list';
import { ApiClientService } from './core/services/api-client.service';

describe('Logistics Frontend Application', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App, DriversList, VehiclesList, RoutesList],
      providers: [provideRouter(routes)],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the Android M3 shell with Drivers, Vehicles, and Routes navigation', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Drivers Directory');
    expect(compiled.textContent).toContain('Drivers');
    expect(compiled.textContent).toContain('Vehicles');
    expect(compiled.textContent).toContain('Routes');
  });

  it('should manage ETags and detect 412/409/400 status codes in ApiClientService', () => {
    const service = TestBed.inject(ApiClientService);
    service.setETag('driver-1', '"v1-abc123"');
    expect(service.getETag('driver-1')).toBe('"v1-abc123"');
    expect(service.isPreconditionFailed({ responseStatusCode: 412 })).toBe(true);
    expect(service.isConflictError({ responseStatusCode: 409 })).toBe(true);
    expect(service.isValidationError({ responseStatusCode: 400 })).toBe(true);
    expect(service.getProblemDetail({ detail: 'Duplicate VIN' }, 'Fallback')).toBe(
      'Duplicate VIN',
    );
  });

  it('should serialize PATCH requests with application/merge-patch+json without throwing', () => {
    const service = TestBed.inject(ApiClientService);
    const reqInfo = service.client.v1.drivers
      .byDriverId('d1042000-0000-4000-a000-000000000001')
      .toPatchRequestInformation({ status: 'OnLeave' });
    expect(reqInfo.httpMethod).toBe('PATCH');
    expect(reqInfo.content).toBeDefined();
  });

  it('should initialize DriversList, VehiclesList, and RoutesList components cleanly', () => {
    const driversFixture = TestBed.createComponent(DriversList);
    const vehiclesFixture = TestBed.createComponent(VehiclesList);
    const routesFixture = TestBed.createComponent(RoutesList);

    expect(driversFixture.componentInstance).toBeTruthy();
    expect(vehiclesFixture.componentInstance).toBeTruthy();
    expect(routesFixture.componentInstance).toBeTruthy();
  });
});
