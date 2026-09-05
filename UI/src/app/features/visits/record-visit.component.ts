import { Component, OnInit, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { ReferenceService } from '../../core/services/reference.service';
import { DoctorsService } from '../../core/services/doctors.service';
import { VisitsService } from '../../core/services/visits.service';
import { Disease, Doctor, Medicine, RecordVisitRequest } from '../../core/models/models';

@Component({
  selector: 'hg-record-visit',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    MatNativeDateModule,
    PageHeaderComponent,
  ],
  templateUrl: './record-visit.component.html',
  styleUrl: './record-visit.component.scss',
})
export class RecordVisitComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly reference = inject(ReferenceService);
  private readonly doctors = inject(DoctorsService);
  private readonly visits = inject(VisitsService);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);

  readonly diseases = signal<Disease[]>([]);
  readonly medicines = signal<Medicine[]>([]);
  readonly doctorList = signal<Doctor[]>([]);
  readonly saving = signal(false);

  readonly items = new FormArray<FormGroup>([]);

  readonly form = this.fb.nonNullable.group({
    localIdentifier: ['', Validators.required],
    birthYear: this.fb.control<number | null>(null),
    gender: this.fb.control<'M' | 'F' | 'O' | null>(null),
    visitDate: [new Date(), Validators.required],
    attendingDoctorId: this.fb.control<string | null>(null),
    symptoms: [''],
    diseaseIds: this.fb.nonNullable.control<string[]>([], Validators.required),
    prescriptionItems: this.items,
  });

  private newItem(): FormGroup {
    return this.fb.nonNullable.group({
      medicineId: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(0.001)]],
    });
  }

  ngOnInit(): void {
    this.reference.diseases().subscribe((d) => this.diseases.set(d));
    this.reference.medicines({ pageSize: 200 }).subscribe((m) => this.medicines.set(m.items));
    this.doctors.search().subscribe((d) => this.doctorList.set(d));
    this.addItem();
  }

  addItem(): void {
    this.items.push(this.newItem());
  }

  removeItem(i: number): void {
    this.items.removeAt(i);
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const payload: RecordVisitRequest = {
      localIdentifier: v.localIdentifier,
      birthYear: v.birthYear,
      gender: v.gender,
      visitDate: this.toDateOnly(v.visitDate),
      symptoms: v.symptoms || null,
      attendingDoctorId: v.attendingDoctorId,
      diseaseIds: v.diseaseIds,
      prescriptionItems: (v.prescriptionItems as { medicineId: string; quantity: number }[]).filter(
        (x) => x.medicineId,
      ),
    };
    this.saving.set(true);
    this.visits
      .record(payload)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.snack.open('Visit recorded — stock issued.', 'OK', { duration: 3500 });
          this.router.navigate(['/visits']);
        },
      });
  }

  private toDateOnly(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(
      d.getDate(),
    ).padStart(2, '0')}`;
  }
}
