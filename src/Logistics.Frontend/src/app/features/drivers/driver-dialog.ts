import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import type {
  Driver,
  DriverStatus,
  LicenseClass,
} from '../../../../../Logistics.ClientLibrary/models/index';

export interface DriverDialogData {
  readonly driver?: Driver;
}

export interface DriverDialogResult {
  readonly employeeNumber: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly email: string;
  readonly phone: string;
  readonly licenseNumber: string;
  readonly licenseClass: LicenseClass;
  readonly licenseExpiryDate: string;
  readonly status: DriverStatus;
}

@Component({
  selector: 'app-driver-dialog',
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
      <mat-icon aria-hidden="true">{{ isEditMode ? 'edit' : 'person_add' }}</mat-icon>
      <span>{{ isEditMode ? 'Edit Driver Profile' : 'Register New Driver' }}</span>
    </h2>

    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="dialog-body">
        <div class="form-row two-col">
          <mat-form-field appearance="outline">
            <mat-label>Employee Number</mat-label>
            <input matInput formControlName="employeeNumber" placeholder="EMP-1042" />
            @if (form.controls.employeeNumber.hasError('required')) {
              <mat-error>Employee number is required</mat-error>
            } @else if (form.controls.employeeNumber.hasError('minlength')) {
              <mat-error>Minimum 2 characters</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Employment Status</mat-label>
            <mat-select formControlName="status">
              @for (st of statuses; track st.value) {
                <mat-option [value]="st.value">{{ st.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>

        <div class="form-row two-col">
          <mat-form-field appearance="outline">
            <mat-label>First Name</mat-label>
            <input matInput formControlName="firstName" placeholder="Marcus" />
            @if (form.controls.firstName.hasError('required')) {
              <mat-error>First name is required</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Last Name</mat-label>
            <input matInput formControlName="lastName" placeholder="Vance" />
            @if (form.controls.lastName.hasError('required')) {
              <mat-error>Last name is required</mat-error>
            }
          </mat-form-field>
        </div>

        <div class="form-row two-col">
          <mat-form-field appearance="outline">
            <mat-label>Email Address</mat-label>
            <input
              matInput
              type="email"
              formControlName="email"
              placeholder="mvance@logistics.com"
            />
            @if (form.controls.email.hasError('required')) {
              <mat-error>Email is required</mat-error>
            } @else if (form.controls.email.hasError('email')) {
              <mat-error>Enter a valid email address</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Phone (E.164)</mat-label>
            <input matInput type="tel" formControlName="phone" placeholder="+15552345678" />
            @if (form.controls.phone.hasError('required')) {
              <mat-error>Phone number is required</mat-error>
            } @else if (form.controls.phone.hasError('pattern')) {
              <mat-error>Use E.164 format (e.g. +15552345678)</mat-error>
            }
          </mat-form-field>
        </div>

        <div class="form-row three-col">
          <mat-form-field appearance="outline">
            <mat-label>CDL License Number</mat-label>
            <input matInput formControlName="licenseNumber" placeholder="DL-88492019-IL" />
            @if (form.controls.licenseNumber.hasError('required')) {
              <mat-error>License number is required</mat-error>
            } @else if (form.controls.licenseNumber.hasError('minlength')) {
              <mat-error>At least 4 characters</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>License Class</mat-label>
            <mat-select formControlName="licenseClass">
              @for (cls of licenseClasses; track cls.value) {
                <mat-option [value]="cls.value">{{ cls.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>License Expiry Date</mat-label>
            <input matInput type="date" formControlName="licenseExpiryDate" />
            @if (form.controls.licenseExpiryDate.hasError('required')) {
              <mat-error>Expiry date is required</mat-error>
            } @else if (form.controls.licenseExpiryDate.hasError('pattern')) {
              <mat-error>Use YYYY-MM-DD format</mat-error>
            }
          </mat-form-field>
        </div>
      </mat-dialog-content>

      <mat-dialog-actions align="end" class="dialog-actions">
        <button mat-button type="button" (click)="close()">Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid">
          {{ isEditMode ? 'Save Changes' : 'Register Driver' }}
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
export class DriverDialog {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<DriverDialog, DriverDialogResult>);
  protected readonly data = inject<DriverDialogData | null>(MAT_DIALOG_DATA, { optional: true });

  protected readonly isEditMode = Boolean(this.data?.driver);

  protected readonly statuses: ReadonlyArray<{ value: DriverStatus; label: string }> = [
    { value: 'Active', label: 'Active' },
    { value: 'OnLeave', label: 'On Leave' },
    { value: 'Inactive', label: 'Inactive' },
    { value: 'Suspended', label: 'Suspended' },
  ];

  protected readonly licenseClasses: ReadonlyArray<{ value: LicenseClass; label: string }> = [
    { value: 'ClassA', label: 'CDL Class A (Tractor-Trailer)' },
    { value: 'ClassB', label: 'CDL Class B (Straight/Box Truck)' },
    { value: 'ClassC', label: 'CDL Class C (HazMat/Special)' },
    { value: 'Standard', label: 'Standard Commercial' },
  ];

  protected readonly form = this.fb.nonNullable.group({
    employeeNumber: [
      this.data?.driver?.employeeNumber ?? '',
      [Validators.required, Validators.minLength(2), Validators.maxLength(32)],
    ],
    firstName: [
      this.data?.driver?.firstName ?? '',
      [Validators.required, Validators.minLength(1), Validators.maxLength(80)],
    ],
    lastName: [
      this.data?.driver?.lastName ?? '',
      [Validators.required, Validators.minLength(1), Validators.maxLength(80)],
    ],
    email: [
      this.data?.driver?.email ?? '',
      [Validators.required, Validators.email, Validators.maxLength(254)],
    ],
    phone: [
      this.data?.driver?.phone ?? '+1',
      [Validators.required, Validators.pattern(/^\+?[1-9]\d{1,14}$/)],
    ],
    licenseNumber: [
      this.data?.driver?.licenseNumber ?? '',
      [Validators.required, Validators.minLength(4), Validators.maxLength(32)],
    ],
    licenseClass: [
      (this.data?.driver?.licenseClass ?? 'ClassA') as LicenseClass,
      [Validators.required],
    ],
    licenseExpiryDate: [
      this.data?.driver?.licenseExpiryDate ?? '2028-12-31',
      [
        Validators.required,
        Validators.pattern(/^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$/),
      ],
    ],
    status: [(this.data?.driver?.status ?? 'Active') as DriverStatus, [Validators.required]],
  });

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
