import { Component, afterNextRender, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatRippleModule } from '@angular/material/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiClientService } from '../../core/services/api-client.service';
import { VehicleDialog, type VehicleDialogResult } from './vehicle-dialog';
import type {
  Vehicle,
  VehicleStatus,
  VehicleType,
} from '../../../../../Logistics.ClientLibrary/models/index';

@Component({
  selector: 'app-vehicles-list',
  imports: [
    DecimalPipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatSnackBarModule,
    MatRippleModule,
    MatTooltipModule,
  ],
  templateUrl: './vehicles-list.html',
  styleUrl: './vehicles-list.css',
})
export class VehiclesList {
  private readonly apiService = inject(ApiClientService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly vehicles = signal<Vehicle[]>([]);
  protected readonly loading = signal<boolean>(true);
  protected readonly loadingMore = signal<boolean>(false);
  protected readonly nextPageToken = signal<string | null>(null);
  protected readonly statusFilter = signal<VehicleStatus | 'All'>('All');
  protected readonly typeFilter = signal<VehicleType | 'All'>('All');

  protected readonly statusOptions: ReadonlyArray<{ value: VehicleStatus | 'All'; label: string }> =
    [
      { value: 'All', label: 'All Units' },
      { value: 'Available', label: 'Available' },
      { value: 'InMaintenance', label: 'In Maintenance' },
      { value: 'OutOfService', label: 'Out of Service' },
      { value: 'Retired', label: 'Retired' },
    ];

  protected readonly typeOptions: ReadonlyArray<{ value: VehicleType | 'All'; label: string }> = [
    { value: 'All', label: 'All Types' },
    { value: 'SemiTractor', label: 'Semi-Tractor' },
    { value: 'BoxTruck', label: 'Box Truck' },
    { value: 'RefrigeratedTruck', label: 'Reefer' },
    { value: 'Flatbed', label: 'Flatbed' },
    { value: 'Van', label: 'Cargo Van' },
  ];

  protected readonly filteredVehicles = computed(() => {
    const items = this.vehicles();
    const status = this.statusFilter();
    const type = this.typeFilter();
    const query = this.apiService.globalSearch().trim().toLowerCase();

    return items.filter((v) => {
      if (status !== 'All' && v.status !== status) {
        return false;
      }
      if (type !== 'All' && v.type !== type) {
        return false;
      }
      if (query) {
        const haystack =
          `${v.make ?? ''} ${v.model ?? ''} ${v.year ?? ''} ${v.licensePlate ?? ''} ${v.vin ?? ''} ${v.type ?? ''}`.toLowerCase();
        return haystack.includes(query);
      }
      return true;
    });
  });

  protected readonly fleetStats = computed(() => {
    const all = this.vehicles();
    const totalPayloadTons =
      all.reduce((acc, v) => acc + (v.capacityKg ?? 0), 0) / 1000;
    return {
      total: all.length,
      available: all.filter((v) => v.status === 'Available').length,
      maintenance: all.filter((v) => v.status === 'InMaintenance').length,
      totalPayloadTons: Math.round(totalPayloadTons * 10) / 10,
    };
  });

  constructor() {
    afterNextRender(() => {
      void this.loadVehicles();
    });
  }

  protected async loadVehicles(append = false): Promise<void> {
    if (append) {
      this.loadingMore.set(true);
    } else {
      this.loading.set(true);
    }

    try {
      const token = append ? (this.nextPageToken() ?? undefined) : undefined;
      const response = await this.apiService.client.v1.vehicles.get({
        queryParameters: {
          pageSize: 20,
          pageToken: token,
        },
      });

      const fetched = response?.items ?? [];
      if (append) {
        this.vehicles.update((prev) => [...prev, ...fetched]);
      } else if (fetched.length > 0) {
        this.vehicles.set(fetched);
      } else {
        this.vehicles.set(this.apiService.getFallbackVehicles());
      }
      this.nextPageToken.set(response?.nextPageToken ?? null);
    } catch {
      this.apiService.backendConnected.set(false);
      if (!append) {
        this.vehicles.set(this.apiService.getFallbackVehicles());
      }
    } finally {
      this.loading.set(false);
      this.loadingMore.set(false);
    }
  }

  protected setStatusFilter(status: VehicleStatus | 'All'): void {
    this.statusFilter.set(status);
  }

  protected setTypeFilter(type: VehicleType | 'All'): void {
    this.typeFilter.set(type);
  }

  protected getVehicleIcon(type?: VehicleType | null): string {
    switch (type) {
      case 'SemiTractor':
        return 'local_shipping';
      case 'RefrigeratedTruck':
        return 'ac_unit';
      case 'Van':
        return 'airport_shuttle';
      case 'Flatbed':
        return 'rv_hookup';
      default:
        return 'fire_truck';
    }
  }

  protected getPayloadPercent(kg?: number | null): number {
    const val = kg ?? 0;
    return Math.min(100, Math.round((val / 22000) * 100));
  }

  protected getVolumePercent(m3?: number | null): number {
    const val = m3 ?? 0;
    return Math.min(100, Math.round((val / 100) * 100));
  }

  protected copyVin(vin?: string | null): void {
    if (!vin) {
      return;
    }
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      void navigator.clipboard.writeText(vin);
    }
    this.snackBar.open(`VIN ${vin} copied to clipboard`, 'OK', { duration: 2500 });
  }

  protected openCreateDialog(): void {
    const ref = this.dialog.open<VehicleDialog, undefined, VehicleDialogResult>(VehicleDialog, {
      width: '620px',
      maxWidth: '95vw',
    });

    ref.afterClosed().subscribe((result) => {
      if (result) {
        void this.createVehicle(result);
      }
    });
  }

  protected openEditDialog(vehicle: Vehicle): void {
    const ref = this.dialog.open<VehicleDialog, { vehicle: Vehicle }, VehicleDialogResult>(
      VehicleDialog,
      {
        width: '620px',
        maxWidth: '95vw',
        data: { vehicle },
      },
    );

    ref.afterClosed().subscribe((result) => {
      if (result && vehicle.id) {
        void this.updateVehicle(vehicle.id, result);
      }
    });
  }

  protected async toggleMaintenance(vehicle: Vehicle): Promise<void> {
    if (!vehicle.id) {
      return;
    }
    const nextStatus: VehicleStatus =
      vehicle.status === 'Available' ? 'InMaintenance' : 'Available';

    try {
      const updated = await this.apiService.client.v1.vehicles
        .byVehicleId(vehicle.id)
        .patch({ status: nextStatus });
      if (updated) {
        this.vehicles.update((list) => list.map((v) => (v.id === vehicle.id ? updated : v)));
      }
    } catch (err: unknown) {
      if (this.apiService.isPreconditionFailed(err)) {
        const snack = this.snackBar.open(
          'Vehicle record was modified by another dispatcher (412 Conflict).',
          'Reload Latest',
          { duration: 6000 },
        );
        snack.onAction().subscribe(() => {
          void this.loadVehicles();
        });
        return;
      }
      this.vehicles.update((list) =>
        list.map((v) => (v.id === vehicle.id ? { ...v, status: nextStatus } : v)),
      );
    }
    this.snackBar.open(
      `${vehicle.make} ${vehicle.model} marked as ${nextStatus}`,
      'Dismiss',
      { duration: 3000 },
    );
  }

  protected async deleteVehicle(vehicle: Vehicle): Promise<void> {
    if (!vehicle.id) {
      return;
    }
    try {
      await this.apiService.client.v1.vehicles.byVehicleId(vehicle.id).delete();
    } catch (err: unknown) {
      if (this.apiService.isPreconditionFailed(err)) {
        const snack = this.snackBar.open(
          'Vehicle record was modified by another dispatcher (412 Conflict).',
          'Reload Latest',
          { duration: 6000 },
        );
        snack.onAction().subscribe(() => {
          void this.loadVehicles();
        });
        return;
      }
    }
    this.vehicles.update((list) => list.filter((v) => v.id !== vehicle.id));
    this.snackBar.open(
      `Removed ${vehicle.year} ${vehicle.make} ${vehicle.model}`,
      'Dismiss',
      { duration: 3500 },
    );
  }

  private async createVehicle(payload: VehicleDialogResult): Promise<void> {
    try {
      const created = await this.apiService.client.v1.vehicles.post(payload);
      if (created) {
        this.vehicles.update((list) => [created, ...list]);
        this.snackBar.open(
          `Added ${created.year} ${created.make} ${created.model}`,
          'Close',
          { duration: 3500 },
        );
        return;
      }
    } catch (err: unknown) {
      if (this.apiService.isConflictError(err) || this.apiService.isValidationError(err)) {
        const detail = this.apiService.getProblemDetail(
          err,
          'A vehicle with the same VIN or license plate already exists.',
        );
        this.snackBar.open(detail, 'Dismiss', { duration: 5000 });
        return;
      }

      this.apiService.backendConnected.set(false);
      const localVehicle: Vehicle = {
        id: crypto.randomUUID(),
        ...payload,
      };
      this.vehicles.update((list) => [localVehicle, ...list]);
      this.snackBar.open(
        `Added ${payload.year} ${payload.make} ${payload.model} (local cache)`,
        'Close',
        { duration: 3500 },
      );
    }
  }

  private async updateVehicle(vehicleId: string, payload: VehicleDialogResult): Promise<void> {
    try {
      const updated = await this.apiService.client.v1.vehicles
        .byVehicleId(vehicleId)
        .put(payload);
      if (updated) {
        this.vehicles.update((list) => list.map((v) => (v.id === vehicleId ? updated : v)));
        this.snackBar.open('Vehicle specifications updated', 'OK', { duration: 3000 });
      }
    } catch (err: unknown) {
      if (this.apiService.isPreconditionFailed(err)) {
        const snack = this.snackBar.open(
          'Vehicle record was modified by another dispatcher (412 Conflict).',
          'Reload Latest',
          { duration: 6000 },
        );
        snack.onAction().subscribe(() => {
          void this.loadVehicles();
        });
        return;
      }

      if (this.apiService.isConflictError(err) || this.apiService.isValidationError(err)) {
        const detail = this.apiService.getProblemDetail(
          err,
          'Could not update vehicle due to a duplicate VIN or license plate.',
        );
        this.snackBar.open(detail, 'Dismiss', { duration: 5000 });
        return;
      }

      this.apiService.backendConnected.set(false);
      this.vehicles.update((list) =>
        list.map((v) => (v.id === vehicleId ? { ...v, ...payload } : v)),
      );
      this.snackBar.open('Vehicle updated in local state', 'OK', { duration: 3000 });
    }
  }
}
