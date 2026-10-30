import { Component, afterNextRender, computed, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatBottomSheet, MatBottomSheetModule } from '@angular/material/bottom-sheet';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatRippleModule } from '@angular/material/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiClientService } from '../../core/services/api-client.service';
import { DriverDialog, type DriverDialogResult } from './driver-dialog';
import { DriverSheet, type DriverSheetAction } from './driver-sheet';
import type {
  Driver,
  DriverStatus,
  LicenseClass,
} from '../../../../../Logistics.ClientLibrary/models/index';

@Component({
  selector: 'app-drivers-list',
  imports: [
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatDialogModule,
    MatBottomSheetModule,
    MatSnackBarModule,
    MatRippleModule,
    MatTooltipModule,
  ],
  templateUrl: './drivers-list.html',
  styleUrl: './drivers-list.css',
})
export class DriversList {
  private readonly apiService = inject(ApiClientService);
  private readonly dialog = inject(MatDialog);
  private readonly bottomSheet = inject(MatBottomSheet);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly drivers = signal<Driver[]>([]);
  protected readonly loading = signal<boolean>(true);
  protected readonly loadingMore = signal<boolean>(false);
  protected readonly nextPageToken = signal<string | null>(null);
  protected readonly statusFilter = signal<DriverStatus | 'All'>('All');
  protected readonly licenseFilter = signal<LicenseClass | 'All'>('All');

  protected readonly statusOptions: ReadonlyArray<{ value: DriverStatus | 'All'; label: string }> =
    [
      { value: 'All', label: 'All Drivers' },
      { value: 'Active', label: 'Active' },
      { value: 'OnLeave', label: 'On Leave' },
      { value: 'Suspended', label: 'Suspended' },
      { value: 'Inactive', label: 'Inactive' },
    ];

  protected readonly licenseOptions: ReadonlyArray<{ value: LicenseClass | 'All'; label: string }> =
    [
      { value: 'All', label: 'Any CDL' },
      { value: 'ClassA', label: 'Class A' },
      { value: 'ClassB', label: 'Class B' },
      { value: 'ClassC', label: 'Class C' },
      { value: 'Standard', label: 'Standard' },
    ];

  protected readonly filteredDrivers = computed(() => {
    const items = this.drivers();
    const status = this.statusFilter();
    const license = this.licenseFilter();
    const query = this.apiService.globalSearch().trim().toLowerCase();

    return items.filter((d) => {
      if (status !== 'All' && d.status !== status) {
        return false;
      }
      if (license !== 'All' && d.licenseClass !== license) {
        return false;
      }
      if (query) {
        const haystack =
          `${d.firstName ?? ''} ${d.lastName ?? ''} ${d.employeeNumber ?? ''} ${d.licenseNumber ?? ''} ${d.email ?? ''}`.toLowerCase();
        return haystack.includes(query);
      }
      return true;
    });
  });

  protected readonly kpiSummary = computed(() => {
    const all = this.drivers();
    return {
      total: all.length,
      active: all.filter((d) => d.status === 'Active').length,
      onLeave: all.filter((d) => d.status === 'OnLeave').length,
      suspended: all.filter((d) => d.status === 'Suspended').length,
    };
  });

  constructor() {
    afterNextRender(() => {
      void this.loadDrivers();
    });
  }

  protected async loadDrivers(append = false): Promise<void> {
    if (append) {
      this.loadingMore.set(true);
    } else {
      this.loading.set(true);
    }

    try {
      const token = append ? (this.nextPageToken() ?? undefined) : undefined;
      const response = await this.apiService.client.v1.drivers.get({
        queryParameters: {
          pageSize: 20,
          pageToken: token,
        },
      });

      const fetched = response?.items ?? [];
      if (append) {
        this.drivers.update((prev) => [...prev, ...fetched]);
      } else if (fetched.length > 0) {
        this.drivers.set(fetched);
      } else {
        this.drivers.set(this.apiService.getFallbackDrivers());
      }
      this.nextPageToken.set(response?.nextPageToken ?? null);
    } catch {
      this.apiService.backendConnected.set(false);
      if (!append) {
        this.drivers.set(this.apiService.getFallbackDrivers());
      }
    } finally {
      this.loading.set(false);
      this.loadingMore.set(false);
    }
  }

  protected setStatusFilter(status: DriverStatus | 'All'): void {
    this.statusFilter.set(status);
  }

  protected setLicenseFilter(license: LicenseClass | 'All'): void {
    this.licenseFilter.set(license);
  }

  protected isExpiringSoon(dateStr?: string | null): boolean {
    if (!dateStr) {
      return false;
    }
    return dateStr < '2027-01-01';
  }

  protected openInspectSheet(driver: Driver): void {
    const ref = this.bottomSheet.open<DriverSheet, Driver, DriverSheetAction>(DriverSheet, {
      data: driver,
      ariaLabel: `Driver details for ${driver.firstName} ${driver.lastName}`,
    });

    ref.afterDismissed().subscribe((result) => {
      if (!result) {
        return;
      }
      if (result.action === 'edit') {
        this.openEditDialog(result.driver);
      } else if (result.action === 'toggle-status') {
        void this.toggleDriverStatus(result.driver);
      } else if (result.action === 'delete') {
        void this.deleteDriver(result.driver);
      }
    });
  }

  protected openCreateDialog(): void {
    const ref = this.dialog.open<DriverDialog, undefined, DriverDialogResult>(DriverDialog, {
      width: '620px',
      maxWidth: '95vw',
    });

    ref.afterClosed().subscribe((result) => {
      if (result) {
        void this.createDriver(result);
      }
    });
  }

  protected openEditDialog(driver: Driver): void {
    const ref = this.dialog.open<DriverDialog, { driver: Driver }, DriverDialogResult>(
      DriverDialog,
      {
        width: '620px',
        maxWidth: '95vw',
        data: { driver },
      },
    );

    ref.afterClosed().subscribe((result) => {
      if (result && driver.id) {
        void this.updateDriver(driver.id, result);
      }
    });
  }

  private async createDriver(payload: DriverDialogResult): Promise<void> {
    try {
      const created = await this.apiService.client.v1.drivers.post(payload);
      if (created) {
        this.drivers.update((list) => [created, ...list]);
        this.snackBar.open(`Driver ${created.firstName} ${created.lastName} registered`, 'Close', {
          duration: 3500,
        });
        return;
      }
    } catch {
      const localDriver: Driver = {
        id: crypto.randomUUID(),
        ...payload,
      };
      this.drivers.update((list) => [localDriver, ...list]);
      this.snackBar.open(
        `Driver ${payload.firstName} ${payload.lastName} added (local cache)`,
        'Close',
        { duration: 3500 },
      );
    }
  }

  private async updateDriver(driverId: string, payload: DriverDialogResult): Promise<void> {
    try {
      const updated = await this.apiService.client.v1.drivers.byDriverId(driverId).put(payload);
      if (updated) {
        this.drivers.update((list) => list.map((d) => (d.id === driverId ? updated : d)));
        this.snackBar.open('Driver profile updated', 'OK', { duration: 3000 });
      }
    } catch (err: unknown) {
      if (this.apiService.isPreconditionFailed(err)) {
        const snack = this.snackBar.open(
          'Record was modified by another dispatcher (412 Conflict).',
          'Reload Latest',
          { duration: 6000 },
        );
        snack.onAction().subscribe(() => {
          void this.loadDrivers();
        });
        return;
      }

      this.drivers.update((list) =>
        list.map((d) => (d.id === driverId ? { ...d, ...payload } : d)),
      );
      this.snackBar.open('Driver profile updated in local state', 'OK', { duration: 3000 });
    }
  }

  private async toggleDriverStatus(driver: Driver): Promise<void> {
    const nextStatus: DriverStatus = driver.status === 'Active' ? 'OnLeave' : 'Active';
    if (!driver.id) {
      return;
    }

    try {
      const updated = await this.apiService.client.v1.drivers
        .byDriverId(driver.id)
        .patch({ status: nextStatus });
      if (updated) {
        this.drivers.update((list) => list.map((d) => (d.id === driver.id ? updated : d)));
      }
    } catch {
      this.drivers.update((list) =>
        list.map((d) => (d.id === driver.id ? { ...d, status: nextStatus } : d)),
      );
    }
    this.snackBar.open(`Driver status changed to ${nextStatus}`, 'Undo', { duration: 3500 });
  }

  private async deleteDriver(driver: Driver): Promise<void> {
    if (!driver.id) {
      return;
    }
    try {
      await this.apiService.client.v1.drivers.byDriverId(driver.id).delete();
    } catch {
      // Proceed with local removal if backend is offline
    }
    this.drivers.update((list) => list.filter((d) => d.id !== driver.id));
    this.snackBar.open(`Removed ${driver.firstName} ${driver.lastName}`, 'Dismiss', {
      duration: 3500,
    });
  }
}
