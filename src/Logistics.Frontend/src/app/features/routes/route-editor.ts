import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CdkDragDrop,
  DragDropModule,
  moveItemInArray,
} from '@angular/cdk/drag-drop';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import type {
  Route,
  RouteStatus,
  RouteWaypoint,
  StopType,
} from '../../../../../Logistics.ClientLibrary/models/index';

export interface RouteEditorData {
  readonly route?: Route;
}

export interface RouteEditorResult {
  readonly code: string;
  readonly name: string;
  readonly description: string;
  readonly status: RouteStatus;
  readonly estimatedDistanceKm: number;
  readonly estimatedDurationMinutes: number;
  readonly waypoints: RouteWaypoint[];
}

@Component({
  selector: 'app-route-editor',
  imports: [
    ReactiveFormsModule,
    DragDropModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    MatTooltipModule,
  ],
  template: `
    <h2 mat-dialog-title class="dialog-header">
      <mat-icon aria-hidden="true">route</mat-icon>
      <span>{{ isEditMode ? 'Route & Waypoint Planner' : 'Create Dispatch Route' }}</span>
    </h2>

    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="dialog-body">
        <div class="form-row three-col">
          <mat-form-field appearance="outline">
            <mat-label>Route Code</mat-label>
            <input
              matInput
              formControlName="code"
              placeholder="RT-CHI-MSP-01"
              (input)="onCodeInput($event)"
            />
            @if (form.controls.code.hasError('required')) {
              <mat-error>Route code is required</mat-error>
            } @else if (form.controls.code.hasError('pattern')) {
              <mat-error>Use uppercase letters, digits & hyphens</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Dispatch Status</mat-label>
            <mat-select formControlName="status">
              @for (st of statuses; track st.value) {
                <mat-option [value]="st.value">{{ st.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Est. Distance (km)</mat-label>
            <input matInput type="number" formControlName="estimatedDistanceKm" min="0" step="5" />
          </mat-form-field>
        </div>

        <div class="form-row two-col">
          <mat-form-field appearance="outline">
            <mat-label>Route Corridor Name</mat-label>
            <input
              matInput
              formControlName="name"
              placeholder="Chicago Depot to Minneapolis Hub"
            />
            @if (form.controls.name.hasError('required')) {
              <mat-error>Route name is required</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Est. Duration (Minutes)</mat-label>
            <input
              matInput
              type="number"
              formControlName="estimatedDurationMinutes"
              min="0"
              step="15"
            />
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Corridor Notes & Instructions</mat-label>
          <input
            matInput
            formControlName="description"
            placeholder="Primary freight corridor with cross-dock stops"
          />
        </mat-form-field>

        <mat-divider />

        <!-- Interactive Drag & Drop Waypoint Planner -->
        <div class="waypoints-header">
          <div>
            <h3 class="waypoints-title">
              Stops & Waypoint Sequence ({{ waypoints().length }} stops)
            </h3>
            <span class="waypoints-subtitle">
              Drag handle <mat-icon class="inline-icon">drag_indicator</mat-icon> to reorder stop
              progression (minimum 2 stops required)
            </span>
          </div>
          <button mat-tonal-button type="button" (click)="addWaypoint()">
            <mat-icon>add_location_alt</mat-icon>
            <span>Add Stop</span>
          </button>
        </div>

        <div
          cdkDropList
          class="waypoints-list"
          (cdkDropListDropped)="onDropWaypoint($event)"
          role="list"
          aria-label="Route Waypoints Sequence"
        >
          @for (wp of waypoints(); track wp.sequenceNumber; let idx = $index) {
            <div class="waypoint-item" cdkDrag role="listitem">
              <button
                type="button"
                class="drag-handle"
                cdkDragHandle
                aria-label="Drag to reorder stop"
                matTooltip="Drag to reorder sequence"
              >
                <mat-icon>drag_indicator</mat-icon>
              </button>

              <div class="seq-badge" [class]="'stop-' + (wp.stopType ?? 'Waypoint').toLowerCase()">
                #{{ idx + 1 }}
              </div>

              <div class="waypoint-fields">
                <div class="wp-row">
                  <input
                    type="text"
                    class="wp-input label-input"
                    [value]="wp.label ?? ''"
                    (input)="updateWaypointField(idx, 'label', $event)"
                    placeholder="Stop Label (e.g. Rockford Hub)"
                    aria-label="Stop label"
                  />

                  <select
                    class="wp-select"
                    [value]="wp.stopType ?? 'Waypoint'"
                    (change)="updateWaypointStopType(idx, $event)"
                    aria-label="Stop type"
                  >
                    @for (st of stopTypes; track st) {
                      <option [value]="st">{{ st }}</option>
                    }
                  </select>

                  <div class="dwell-field">
                    <input
                      type="number"
                      class="wp-input dwell-input"
                      [value]="wp.dwellTimeMinutes ?? 0"
                      min="0"
                      (input)="updateWaypointNumber(idx, 'dwellTimeMinutes', $event)"
                      aria-label="Dwell time minutes"
                    />
                    <span class="unit-tag">min</span>
                  </div>
                </div>

                <div class="wp-row secondary-wp-row">
                  <input
                    type="text"
                    class="wp-input address-input"
                    [value]="wp.address ?? ''"
                    (input)="updateWaypointField(idx, 'address', $event)"
                    placeholder="Street Address, City, State"
                    aria-label="Stop street address"
                  />
                  <span class="coords-tag">
                    {{ wp.latitude?.toFixed(2) ?? '41.88' }}°N,
                    {{ wp.longitude?.toFixed(2) ?? '-87.63' }}°W
                  </span>
                </div>
              </div>

              <button
                mat-icon-button
                type="button"
                color="warn"
                [disabled]="waypoints().length <= 2"
                (click)="removeWaypoint(idx)"
                [attr.aria-label]="'Remove stop ' + (wp.label ?? idx + 1)"
                matTooltip="Remove stop (minimum 2 required)"
              >
                <mat-icon>delete_outline</mat-icon>
              </button>
            </div>
          }
        </div>
      </mat-dialog-content>

      <mat-dialog-actions align="end" class="dialog-actions">
        <button mat-button type="button" (click)="close()">Cancel</button>
        <button
          mat-flat-button
          type="submit"
          [disabled]="form.invalid || waypoints().length < 2"
        >
          {{ isEditMode ? 'Save Route & Waypoints' : 'Create Dispatch Route' }}
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
      gap: 8px;
      padding-top: 12px !important;
      min-width: min(680px, 90vw);
      max-height: 76vh;
    }
    .form-row {
      display: grid;
      gap: 12px;
    }
    .two-col {
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    }
    .three-col {
      grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
    }
    .full-width {
      width: 100%;
    }
    .waypoints-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-top: 8px;
      flex-wrap: wrap;
    }
    .waypoints-title {
      margin: 0;
      font-size: 0.98rem;
      font-weight: 600;
    }
    .waypoints-subtitle {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 0.78rem;
      color: var(--mat-sys-on-surface-variant, #44474e);
    }
    .inline-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .waypoints-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
      margin-top: 6px;
    }
    .waypoint-item {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 10px 12px;
      border-radius: 14px;
      background-color: var(--mat-sys-surface-container, #efedf0);
      border: 1px solid var(--mat-sys-outline-variant, #c4c6d0);
    }
    .cdk-drag-preview {
      box-sizing: border-box;
      border-radius: 14px;
      box-shadow: var(--mat-sys-level3);
      background-color: var(--mat-sys-surface-container-lowest, #ffffff);
    }
    .cdk-drag-placeholder {
      opacity: 0.35;
    }
    .drag-handle {
      border: none;
      background: transparent;
      cursor: grab;
      color: var(--mat-sys-on-surface-variant, #44474e);
      padding: 4px;
      display: flex;
      align-items: center;
    }
    .seq-badge {
      width: 32px;
      height: 32px;
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 0.8rem;
      background-color: var(--mat-sys-primary-container, #d7e3ff);
      color: var(--mat-sys-on-primary-container, #00458f);
      flex-shrink: 0;
    }
    .seq-badge.stop-origin {
      background-color: #d4edda;
      color: #145523;
    }
    .seq-badge.stop-destination {
      background-color: #f8d7da;
      color: #842029;
    }
    .waypoint-fields {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 6px;
      min-width: 0;
    }
    .wp-row {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
    }
    .wp-input,
    .wp-select {
      height: 34px;
      padding: 0 10px;
      border-radius: 8px;
      border: 1px solid var(--mat-sys-outline-variant, #c4c6d0);
      background-color: var(--mat-sys-surface-container-lowest, #ffffff);
      font-size: 0.85rem;
      color: var(--mat-sys-on-surface, #1a1b1f);
    }
    .label-input {
      flex: 1;
      min-width: 150px;
      font-weight: 600;
    }
    .address-input {
      flex: 1;
      min-width: 180px;
      font-size: 0.8rem;
    }
    .dwell-field {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .dwell-input {
      width: 64px;
      text-align: right;
    }
    .unit-tag,
    .coords-tag {
      font-size: 0.74rem;
      color: var(--mat-sys-on-surface-variant, #44474e);
      font-family: monospace;
    }
    .dialog-actions {
      padding: 12px 24px 20px;
    }
  `,
})
export class RouteEditor {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<RouteEditor, RouteEditorResult>);
  protected readonly data = inject<RouteEditorData | null>(MAT_DIALOG_DATA, { optional: true });

  protected readonly isEditMode = Boolean(this.data?.route);

  protected readonly statuses: ReadonlyArray<{ value: RouteStatus; label: string }> = [
    { value: 'Active', label: 'Active' },
    { value: 'Draft', label: 'Draft' },
    { value: 'Archived', label: 'Archived' },
  ];

  protected readonly stopTypes: readonly StopType[] = [
    'Origin',
    'Pickup',
    'Delivery',
    'Waypoint',
    'Destination',
  ];

  protected readonly waypoints = signal<RouteWaypoint[]>(
    this.data?.route?.waypoints?.length
      ? this.data.route.waypoints.map((w, idx) => ({
          ...w,
          sequenceNumber: w.sequenceNumber ?? idx + 1,
        }))
      : [
          {
            sequenceNumber: 1,
            label: 'Origin Logistics Depot',
            address: '2800 S Ashland Ave, Chicago, IL',
            latitude: 41.8414,
            longitude: -87.6658,
            stopType: 'Origin',
            dwellTimeMinutes: 0,
          },
          {
            sequenceNumber: 2,
            label: 'Destination Regional Hub',
            address: '1900 Broadway St NE, Minneapolis, MN',
            latitude: 45.0031,
            longitude: -93.2492,
            stopType: 'Destination',
            dwellTimeMinutes: 0,
          },
        ],
  );

  protected readonly form = this.fb.nonNullable.group({
    code: [
      this.data?.route?.code ?? 'RT-NEW-01',
      [
        Validators.required,
        Validators.minLength(2),
        Validators.maxLength(32),
        Validators.pattern(/^[A-Z0-9-]+$/),
      ],
    ],
    name: [
      this.data?.route?.name ?? '',
      [Validators.required, Validators.minLength(1), Validators.maxLength(120)],
    ],
    description: [this.data?.route?.description ?? '', [Validators.maxLength(500)]],
    status: [(this.data?.route?.status ?? 'Active') as RouteStatus, [Validators.required]],
    estimatedDistanceKm: [
      this.data?.route?.estimatedDistanceKm ?? 450,
      [Validators.required, Validators.min(0)],
    ],
    estimatedDurationMinutes: [
      this.data?.route?.estimatedDurationMinutes ?? 300,
      [Validators.required, Validators.min(0)],
    ],
  });

  protected onCodeInput(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    if (input) {
      const sanitized = input.value.toUpperCase().replace(/[^A-Z0-9-]/g, '');
      this.form.controls.code.setValue(sanitized, { emitEvent: false });
    }
  }

  protected onDropWaypoint(event: CdkDragDrop<RouteWaypoint[]>): void {
    const copy = [...this.waypoints()];
    moveItemInArray(copy, event.previousIndex, event.currentIndex);
    this.waypoints.set(this.resequence(copy));
  }

  protected addWaypoint(): void {
    const current = [...this.waypoints()];
    const insertIdx = Math.max(1, current.length - 1);
    const newStop: RouteWaypoint = {
      sequenceNumber: insertIdx + 1,
      label: `Stop #${insertIdx + 1} Distribution Point`,
      address: 'Regional Freight Park',
      latitude: 42.5,
      longitude: -89.0,
      stopType: 'Pickup',
      dwellTimeMinutes: 30,
    };
    current.splice(insertIdx, 0, newStop);
    this.waypoints.set(this.resequence(current));
  }

  protected removeWaypoint(index: number): void {
    const current = this.waypoints();
    if (current.length <= 2) {
      return;
    }
    const filtered = current.filter((_, i) => i !== index);
    this.waypoints.set(this.resequence(filtered));
  }

  protected updateWaypointField(
    index: number,
    field: 'label' | 'address',
    event: Event,
  ): void {
    const val = (event.target as HTMLInputElement | null)?.value ?? '';
    this.waypoints.update((list) =>
      list.map((item, i) => (i === index ? { ...item, [field]: val } : item)),
    );
  }

  protected updateWaypointStopType(index: number, event: Event): void {
    const val = ((event.target as HTMLSelectElement | null)?.value ?? 'Waypoint') as StopType;
    this.waypoints.update((list) =>
      list.map((item, i) => (i === index ? { ...item, stopType: val } : item)),
    );
  }

  protected updateWaypointNumber(
    index: number,
    field: 'dwellTimeMinutes',
    event: Event,
  ): void {
    const num = Number((event.target as HTMLInputElement | null)?.value ?? 0);
    this.waypoints.update((list) =>
      list.map((item, i) => (i === index ? { ...item, [field]: Math.max(0, num) } : item)),
    );
  }

  protected submit(): void {
    if (this.form.invalid || this.waypoints().length < 2) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    this.dialogRef.close({
      ...raw,
      waypoints: this.resequence(this.waypoints()),
    });
  }

  protected close(): void {
    this.dialogRef.close();
  }

  private resequence(items: RouteWaypoint[]): RouteWaypoint[] {
    return items.map((item, idx) => ({
      ...item,
      sequenceNumber: idx + 1,
    }));
  }
}
