import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'drivers',
  },
  {
    path: 'drivers',
    loadComponent: () =>
      import('./features/drivers/drivers-list').then((m) => m.DriversList),
    title: 'Drivers Directory • Logistics',
  },
  {
    path: 'vehicles',
    loadComponent: () =>
      import('./features/vehicles/vehicles-list').then((m) => m.VehiclesList),
    title: 'Vehicles Fleet • Logistics',
  },
  {
    path: 'routes',
    loadComponent: () =>
      import('./features/routes/routes-list').then((m) => m.RoutesList),
    title: 'Routes & Dispatch • Logistics',
  },
  {
    path: '**',
    redirectTo: 'drivers',
  },
];
