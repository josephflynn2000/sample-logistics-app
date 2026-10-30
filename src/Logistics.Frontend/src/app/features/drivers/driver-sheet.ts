import { Component, inject } from '@angular/core';
import {
  MAT_BOTTOM_SHEET_DATA,
  MatBottomSheetModule,
  MatBottomSheetRef,
} from '@angular/material/bottom-sheet';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import type { Driver } from '../../../../../Logistics.ClientLibrary/models/index';

export interface DriverSheetAction {
  readonly action: 'edit' | 'delete' | 'toggle-status';
  readonly driver: Driver;
}

@Component({
  selector: 'app-driver-sheet',
  imports: [MatBottomSheetModule, MatButtonModule, MatIconModule, MatDividerModule],
  template: `
    <div class="m3-bottom-sheet">
      <div class="drag-handle" aria-hidden="true"></div>

      <div class="sheet-header">
        <div class="avatar-large" aria-hidden="true">
          {{ (driver.firstName?.[0] ?? '') + (driver.lastName?.[0] ?? '') }}
        </div>
        <div class="header-meta">
          <h3 class="driver-title">{{ driver.firstName }} {{ driver.lastName }}</h3>
          <span class="emp-sub">Employee #{{ driver.employeeNumber }}</span>
        </div>
        <span class="status-badge" [class]="'status-' + (driver.status ?? 'Active').toLowerCase()">
          {{ driver.status }}
        </span>
      </div>

      <mat-divider />

      <div class="sheet-section">
        <h4 class="section-heading">Commercial License & Compliance</h4>
        <div class="info-grid">
          <div class="info-item">
            <span class="info-label">License Number</span>
            <span class="info-value">{{ driver.licenseNumber }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">CDL Class</span>
            <span class="info-value">{{ driver.licenseClass }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Expiration Date</span>
            <span class="info-value">{{ driver.licenseExpiryDate }}</span>
          </div>
        </div>
      </div>

      <mat-divider />

      <div class="sheet-section">
        <h4 class="section-heading">Quick Communication</h4>
        <div class="quick-links">
          <a mat-stroked-button [href]="'tel:' + driver.phone" class="comm-btn">
            <mat-icon>call</mat-icon>
            <span>Call {{ driver.phone }}</span>
          </a>
          <a mat-stroked-button [href]="'mailto:' + driver.email" class="comm-btn">
            <mat-icon>mail</mat-icon>
            <span>Email Driver</span>
          </a>
        </div>
      </div>

      <div class="sheet-footer">
        <button mat-flat-button type="button" (click)="trigger('edit')">
          <mat-icon>edit</mat-icon>
          <span>Edit Profile</span>
        </button>
        <button mat-stroked-button type="button" (click)="trigger('toggle-status')">
          <mat-icon>sync_alt</mat-icon>
          <span>{{ driver.status === 'Active' ? 'Place On Leave' : 'Activate' }}</span>
        </button>
        <button mat-button color="warn" type="button" (click)="trigger('delete')">
          <mat-icon>delete_outline</mat-icon>
          <span>Remove</span>
        </button>
      </div>
    </div>
  `,
  styles: `
    .m3-bottom-sheet {
      padding: 8px 16px 24px;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .drag-handle {
      width: 36px;
      height: 4px;
      border-radius: 2px;
      background-color: var(--mat-sys-outline-variant, #c4c6d0);
      margin: 4px auto 4px;
    }
    .sheet-header {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .avatar-large {
      width: 52px;
      height: 52px;
      border-radius: 16px;
      background-color: var(--mat-sys-primary-container, #d7e3ff);
      color: var(--mat-sys-on-primary-container, #00458f);
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 1.1rem;
    }
    .header-meta {
      flex: 1;
      min-width: 0;
    }
    .driver-title {
      margin: 0;
      font-size: 1.15rem;
      font-weight: 600;
    }
    .emp-sub {
      font-size: 0.82rem;
      color: var(--mat-sys-on-surface-variant, #44474e);
    }
    .section-heading {
      margin: 0 0 10px;
      font-size: 0.78rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--mat-sys-on-surface-variant, #44474e);
    }
    .info-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 12px;
    }
    .info-item {
      display: flex;
      flex-direction: column;
      gap: 2px;
      background-color: var(--mat-sys-surface-container, #efedf0);
      padding: 10px 12px;
      border-radius: 12px;
    }
    .info-label {
      font-size: 0.72rem;
      color: var(--mat-sys-on-surface-variant, #44474e);
    }
    .info-value {
      font-size: 0.88rem;
      font-weight: 600;
    }
    .quick-links {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
    }
    .comm-btn {
      flex: 1;
      border-radius: 12px;
    }
    .sheet-footer {
      display: flex;
      gap: 10px;
      justify-content: flex-end;
      flex-wrap: wrap;
      margin-top: 4px;
    }
  `,
})
export class DriverSheet {
  private readonly sheetRef = inject(MatBottomSheetRef<DriverSheet, DriverSheetAction>);
  protected readonly driver = inject<Driver>(MAT_BOTTOM_SHEET_DATA);

  protected trigger(action: DriverSheetAction['action']): void {
    this.sheetRef.dismiss({ action, driver: this.driver });
  }
}
