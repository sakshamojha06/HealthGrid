import { Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { of, switchMap } from 'rxjs';
import { DoctorsService } from '../../core/services/doctors.service';
import { Doctor, Phc, Specialization } from '../../core/models/models';

export interface DoctorFormData {
  doctor: Doctor | null;
  phcs: Phc[];
  specializations: Specialization[];
}

const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

@Component({
  selector: 'hg-doctor-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './doctor-form-dialog.component.html',
  styleUrl: './doctor-form-dialog.component.scss',
})
export class DoctorFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(DoctorsService);
  readonly data = inject<DoctorFormData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<DoctorFormDialogComponent>);

  readonly days = DAYS;
  readonly saving = signal(false);

  readonly slots = new FormArray<FormGroup>([]);
  readonly form = this.fb.nonNullable.group({
    name: [this.data.doctor?.name ?? '', Validators.required],
    phcId: [this.data.doctor?.phcId ?? '', Validators.required],
    registrationNumber: [this.data.doctor?.registrationNumber ?? ''],
    specializationId: [this.data.doctor?.specializationId ?? ''],
    availability: this.slots,
  });

  constructor() {
    for (const slot of this.data.doctor?.availability ?? []) {
      this.slots.push(
        this.fb.nonNullable.group({
          dayOfWeek: [slot.dayOfWeek],
          startTime: [slot.startTime],
          endTime: [slot.endTime],
        }),
      );
    }
  }

  addSlot(): void {
    this.slots.push(
      this.fb.nonNullable.group({ dayOfWeek: [1], startTime: ['09:00'], endTime: ['13:00'] }),
    );
  }

  removeSlot(i: number): void {
    this.slots.removeAt(i);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body: Partial<Doctor> = {
      name: v.name,
      phcId: v.phcId,
      registrationNumber: v.registrationNumber || null,
      specializationId: v.specializationId || null,
    };
    const slots = v.availability as { dayOfWeek: number; startTime: string; endTime: string }[];
    this.saving.set(true);

    const upsert = this.data.doctor
      ? this.service.update(this.data.doctor.id, body)
      : this.service.create(body);

    upsert
      .pipe(switchMap((doc) => (slots.length ? this.service.setAvailability(doc.id, slots) : of(doc))))
      .subscribe({
        next: (doc) => this.ref.close(doc),
        error: () => this.saving.set(false),
      });
  }
}
