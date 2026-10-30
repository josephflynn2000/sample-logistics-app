import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import type {
  Vehicle,
  VehicleStatus,
  VehicleType,
} from '../../../../../Logistics.ClientLibrary/models/index';

export interface VehicleDialogData {
  readonly vehicle?: Vehicle;
}

export interface VehicleDialogResult {
  readonly vin: string;
  readonly licensePlate: string;
  readonly make: string;
  readonly model: string;
  readonly year: number;
  readonly type: VehicleType;
  readonly capacityKg: number;
  readonly capacityVolumeM3: number;
  readonly status: VehicleStatus;
}

@Component({
  selector: 'app-vehicle-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
  ],
  template: `
    <h2 mat-dialog-title class="dialog-header">
      <mat-icon aria-hidden="true">{{ isEditMode ? 'edit' : 'local_shipping' }}</mat-icon>
      <span>{{ isEditMode ? 'Edit Fleet Vehicle' : 'Add Fleet Vehicle' }}</span>
    </h2>

    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="dialog-body">
        <div class="form-row two-col">
          <mat-form-field appearance="outline">
            <mat-label>17-Character VIN</mat-label>
            <input
              matInput
              formControlName="vin"
              maxlength="17"
              placeholder="1FUJGLDR8PL108921"
              (input)="onVinInput($event)"
            />
            <mat-hint align="end">{{ form.controls.vin.value.length }}/17</mat-hint>
            @if (form.controls.vin.hasError('required')) {
              <mat-error>VIN is required</mat-error>
            } @else if (form.controls.vin.hasError('pattern')) {
              <mat-error>Must be 17 valid characters (A-Z, 0-9, no I/O/Q)</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>License Plate</mat-label>
            <input matInput formControlName="licensePlate" placeholder="IL • 892-TRK" />
            @if (form.controls.licensePlate.hasError('required')) {
              <mat-error>License plate is required</mat-error>
            }
          </mat-form-field>
        </div>

        <div class="form-row three-col">
          <mat-form-field appearance="outline">
            <mat-label>Make</mat-label>
            <input matInput formControlName="make" placeholder="Freightliner" />
            @if (form.controls.make.hasError('required')) {
              <mat-error>Make is required</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Model</mat-label>
            <input matInput formControlName="model" placeholder="Cascadia 126" />
            @if (form.controls.model.hasError('required')) {
              <mat-error>Model is required</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Model Year</mat-label>
            <input matInput type="number" formControlName="year" min="1990" max="2100" />
            @if (form.controls.year.hasError('min') || form.controls.year.hasError('max')) {
              <mat-error>Year must be 1990–2100</mat-error>
            }
          </mat-form-field>
        </div>

        <div class="form-row two-col">
          <mat-form-field appearance="outline">
            <mat-label>Vehicle Classification</mat-label>
            <mat-select formControlName="type">
              @for (tp of vehicleTypes; track tp.value) {
                <mat-option [value]="tp.value">{{ tp.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Operational Status</mat-label>
            <mat-select formControlName="status">
              @for (st of statuses; track st.value) {
                <mat-option [value]="st.value">{{ st.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>

        <div class="form-row two-col">
          <mat-form-field appearance="outline">
            <mat-label>Max Payload Capacity (kg)</mat-label>
            <input matInput type="number" formControlName="capacityKg" min="1" step="100" />
            @if (form.controls.capacityKg.hasError('min')) {
              <mat-error>Must be at least 1 kg</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Cargo Volume Capacity (m³)</mat-label>
            <input matInput type="number" formControlName="capacityVolumeM3" min="0.1" step="0.5" />
            @if (form.controls.capacityVolumeM3.hasError('min')) {
              <mat-error>Must be at least 0.1 m³</mat-error>
            }
          </mat-form-field>
        </div>
      </mat-dialog-content>

      <mat-dialog-actions align="end" class="dialog-actions">
        <button mat-button type="button" (click)="close()">Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid">
          {{ isEditMode ? 'Update Vehicle' : 'Add Vehicle' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .dialog-header {
      display: flex;
      align-items: center;
      gap: 10px;
      margin: 0;
      padding: 20px 24px 8px;
      font-weight: 600;
    }
    .dialog-body {
      display: flex;
      flex-direction: column;
      gap: 6px;
      padding-top: 12px !important;
      min-width: min(560px, 85vw);
    }
    .form-row {
      display: grid;
      gap: 12px;
    }
    .two-col {
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    }
    .three-col {
      grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
    }
    .dialog-actions {
      padding: 12px 24px 20px;
    }
  `,
})
export class VehicleDialog {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<VehicleDialog, VehicleDialogResult>);
  protected readonly data = inject<VehicleDialogData | null>(MAT_DIALOG_DATA, { optional: true });

  protected readonly isEditMode = Boolean(this.data?.vehicle);

  protected readonly vehicleTypes: ReadonlyArray<{ value: VehicleType; label: string }> = [
    { value: 'SemiTractor', label: 'Semi-Tractor (Class 8)' },
    { value: 'BoxTruck', label: 'Box Truck (Straight)' },
    { value: 'RefrigeratedTruck', label: 'Refrigerated Reefer' },
    { value: 'Flatbed', label: 'Flatbed Hauler' },
    { value: 'Van', label: 'Cargo Van' },
  ];

  protected readonly statuses: ReadonlyArray<{ value: VehicleStatus; label: string }> = [
    { value: 'Available', label: 'Available' },
    { value: 'InMaintenance', label: 'In Maintenance' },
    { value: 'OutOfService', label: 'Out of Service' },
    { value: 'Retired', label: 'Retired' },
  ];

  protected readonly form = this.fb.nonNullable.group({
    vin: [
      this.data?.vehicle?.vin ?? '',
      [Validators.required, Validators.pattern(/^[A-HJ-NPR-Z0-9]{17}$/)],
    ],
    licensePlate: [
      this.data?.vehicle?.licensePlate ?? '',
      [Validators.required, Validators.minLength(2), Validators.maxLength(20)],
    ],
    make: [
      this.data?.vehicle?.make ?? '',
      [Validators.required, Validators.minLength(1), Validators.maxLength(60)],
    ],
    model: [
      this.data?.vehicle?.model ?? '',
      [Validators.required, Validators.minLength(1), Validators.maxLength(60)],
    ],
    year: [
      this.data?.vehicle?.year ?? 2024,
      [Validators.required, Validators.min(1990), Validators.max(2100)],
    ],
    type: [
      (this.data?.vehicle?.type ?? 'SemiTractor') as VehicleType,
      [Validators.required],
    ],
    capacityKg: [
      this.data?.vehicle?.capacityKg ?? 18000,
      [Validators.required, Validators.min(1)],
    ],
    capacityVolumeM3: [
      this.data?.vehicle?.capacityVolumeM3 ?? 75,
      [Validators.required, Validators.min(0.1)],
    ],
    status: [
      (this.data?.vehicle?.status ?? 'Available') as VehicleStatus,
      [Validators.required],
    ],
  });

  protected onVinInput(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    if (input) {
      const upper = input.value.toUpperCase().replace(/[^A-HJ-NPR-Z0-9]/g, '');
      this.form.controls.vin.setValue(upper, { emitEvent: false });
    }
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close(this.form.getRawValue());
  }

  protected close(): void {
    this.dialogRef.close();
  }
}
