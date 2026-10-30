import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatRippleModule } from '@angular/material/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiClientService } from '../../services/api-client.service';

export interface NavItem {
  readonly path: string;
  readonly label: string;
  readonly icon: string;
  readonly description: string;
}

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatRippleModule,
    MatBadgeModule,
    MatTooltipModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly apiService = inject(ApiClientService);

  protected readonly currentUrl = signal<string>(this.router.url || '/drivers');
  protected readonly searchExpanded = signal<boolean>(false);

  protected readonly navItems: readonly NavItem[] = [
    {
      path: '/drivers',
      label: 'Drivers',
      icon: 'badge',
      description: 'Manage commercial drivers and CDL status',
    },
    {
      path: '/vehicles',
      label: 'Vehicles',
      icon: 'local_shipping',
      description: 'Fleet specifications, capacity, and maintenance',
    },
    {
      path: '/routes',
      label: 'Routes',
      icon: 'route',
      description: 'Dispatch routes and stop waypoint sequencing',
    },
  ];

  protected readonly activeScreenTitle = computed(() => {
    const url = this.currentUrl();
    if (url.startsWith('/vehicles')) {
      return 'Vehicles Fleet';
    }
    if (url.startsWith('/routes')) {
      return 'Routes & Dispatch';
    }
    return 'Drivers Directory';
  });

  protected readonly activeScreenSubtitle = computed(() => {
    const url = this.currentUrl();
    if (url.startsWith('/vehicles')) {
      return 'Active tractors, box trucks, vans & cargo capacities';
    }
    if (url.startsWith('/routes')) {
      return 'Multi-stop corridors, dwell times & waypoint planner';
    }
    return 'Operator roster, CDL classes & compliance status';
  });

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((event) => {
        this.currentUrl.set(event.urlAfterRedirects);
      });
  }

  protected toggleSearch(): void {
    this.searchExpanded.update((prev) => !prev);
    if (!this.searchExpanded()) {
      this.apiService.globalSearch.set('');
    }
  }

  protected onSearchInput(event: Event): void {
    const target = event.target as HTMLInputElement | null;
    this.apiService.globalSearch.set(target?.value ?? '');
  }

  protected clearSearch(): void {
    this.apiService.globalSearch.set('');
  }
}
