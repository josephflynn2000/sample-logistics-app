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
import { RouteEditor, type RouteEditorResult } from './route-editor';
import type {
  Route,
  RouteStatus,
  StopType,
} from '../../../../../Logistics.ClientLibrary/models/index';

@Component({
  selector: 'app-routes-list',
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
  templateUrl: './routes-list.html',
  styleUrl: './routes-list.css',
})
export class RoutesList {
  private readonly apiService = inject(ApiClientService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly routes = signal<Route[]>([]);
  protected readonly loading = signal<boolean>(true);
  protected readonly loadingMore = signal<boolean>(false);
  protected readonly nextPageToken = signal<string | null>(null);
  protected readonly statusFilter = signal<RouteStatus | 'All'>('All');

  protected readonly statusOptions: ReadonlyArray<{ value: RouteStatus | 'All'; label: string }> = [
    { value: 'All', label: 'All Routes' },
    { value: 'Active', label: 'Active Dispatch' },
    { value: 'Draft', label: 'Draft Planning' },
    { value: 'Archived', label: 'Archived' },
  ];

  protected readonly filteredRoutes = computed(() => {
    const items = this.routes();
    const status = this.statusFilter();
    const query = this.apiService.globalSearch().trim().toLowerCase();

    return items.filter((r) => {
      if (status !== 'All' && r.status !== status) {
        return false;
      }
      if (query) {
        const stopsText = (r.waypoints ?? [])
          .map((w) => `${w.label ?? ''} ${w.address ?? ''}`)
          .join(' ');
        const haystack =
          `${r.code ?? ''} ${r.name ?? ''} ${r.description ?? ''} ${stopsText}`.toLowerCase();
        return haystack.includes(query);
      }
      return true;
    });
  });

  protected readonly routeKpis = computed(() => {
    const all = this.routes();
    const totalKm = all.reduce((sum, r) => sum + (r.estimatedDistanceKm ?? 0), 0);
    const totalStops = all.reduce((sum, r) => sum + (r.waypoints?.length ?? 0), 0);
    return {
      total: all.length,
      active: all.filter((r) => r.status === 'Active').length,
      draft: all.filter((r) => r.status === 'Draft').length,
      totalKm: Math.round(totalKm),
      totalStops,
    };
  });

  constructor() {
    afterNextRender(() => {
      void this.loadRoutes();
    });
  }

  protected async loadRoutes(append = false): Promise<void> {
    if (append) {
      this.loadingMore.set(true);
    } else {
      this.loading.set(true);
    }

    try {
      const token = append ? (this.nextPageToken() ?? undefined) : undefined;
      const response = await this.apiService.client.v1.routes.get({
        queryParameters: {
          pageSize: 20,
          pageToken: token,
        },
      });

      const fetched = response?.items ?? [];
      if (append) {
        this.routes.update((prev) => [...prev, ...fetched]);
      } else if (fetched.length > 0) {
        this.routes.set(fetched);
      } else {
        this.routes.set(this.apiService.getFallbackRoutes());
      }
      this.nextPageToken.set(response?.nextPageToken ?? null);
    } catch {
      this.apiService.backendConnected.set(false);
      if (!append) {
        this.routes.set(this.apiService.getFallbackRoutes());
      }
    } finally {
      this.loading.set(false);
      this.loadingMore.set(false);
    }
  }

  protected setStatusFilter(status: RouteStatus | 'All'): void {
    this.statusFilter.set(status);
  }

  protected formatDuration(minutes?: number | null): string {
    const mins = minutes ?? 0;
    const hrs = Math.floor(mins / 60);
    const rem = mins % 60;
    if (hrs === 0) {
      return `${rem}m`;
    }
    return rem > 0 ? `${hrs}h ${rem}m` : `${hrs}h`;
  }

  protected getStopIcon(type?: StopType | null): string {
    switch (type) {
      case 'Origin':
        return 'trip_origin';
      case 'Pickup':
        return 'inventory_2';
      case 'Delivery':
        return 'local_mall';
      case 'Destination':
        return 'flag';
      default:
        return 'place';
    }
  }

  protected openCreateRoute(): void {
    const ref = this.dialog.open<RouteEditor, undefined, RouteEditorResult>(RouteEditor, {
      width: '740px',
      maxWidth: '96vw',
    });

    ref.afterClosed().subscribe((result) => {
      if (result) {
        void this.createRoute(result);
      }
    });
  }

  protected openManageRoute(route: Route): void {
    const ref = this.dialog.open<RouteEditor, { route: Route }, RouteEditorResult>(RouteEditor, {
      width: '740px',
      maxWidth: '96vw',
      data: { route },
    });

    ref.afterClosed().subscribe((result) => {
      if (result && route.id) {
        void this.updateRoute(route.id, result);
      }
    });
  }

  protected async toggleRouteStatus(route: Route): Promise<void> {
    if (!route.id) {
      return;
    }
    const nextStatus: RouteStatus = route.status === 'Active' ? 'Draft' : 'Active';

    try {
      const updated = await this.apiService.client.v1.routes
        .byRouteId(route.id)
        .patch({ status: nextStatus });
      if (updated) {
        this.routes.update((list) => list.map((r) => (r.id === route.id ? updated : r)));
      }
    } catch {
      this.routes.update((list) =>
        list.map((r) => (r.id === route.id ? { ...r, status: nextStatus } : r)),
      );
    }
    this.snackBar.open(`Route ${route.code} set to ${nextStatus}`, 'OK', { duration: 3000 });
  }

  private async createRoute(payload: RouteEditorResult): Promise<void> {
    try {
      const created = await this.apiService.client.v1.routes.post(payload);
      if (created) {
        this.routes.update((list) => [created, ...list]);
        this.snackBar.open(`Route ${created.code} created`, 'Close', { duration: 3500 });
        return;
      }
    } catch {
      const localRoute: Route = {
        id: crypto.randomUUID(),
        ...payload,
      };
      this.routes.update((list) => [localRoute, ...list]);
      this.snackBar.open(`Route ${payload.code} created (local cache)`, 'Close', {
        duration: 3500,
      });
    }
  }

  private async updateRoute(routeId: string, payload: RouteEditorResult): Promise<void> {
    try {
      const updated = await this.apiService.client.v1.routes.byRouteId(routeId).put(payload);
      if (updated) {
        this.routes.update((list) => list.map((r) => (r.id === routeId ? updated : r)));
        this.snackBar.open('Route & waypoints saved', 'OK', { duration: 3000 });
      }
    } catch (err: unknown) {
      if (this.apiService.isPreconditionFailed(err)) {
        const snack = this.snackBar.open(
          'Route was modified by another dispatcher (412 Conflict).',
          'Reload Latest',
          { duration: 6000 },
        );
        snack.onAction().subscribe(() => {
          void this.loadRoutes();
        });
        return;
      }

      this.routes.update((list) =>
        list.map((r) => (r.id === routeId ? { ...r, ...payload } : r)),
      );
      this.snackBar.open('Route & waypoints updated in local state', 'OK', { duration: 3000 });
    }
  }
}
