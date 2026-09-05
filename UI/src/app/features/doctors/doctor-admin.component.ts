import { Component, OnInit, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { forkJoin } from 'rxjs';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { DoctorsService } from '../../core/services/doctors.service';
import { ReferenceService } from '../../core/services/reference.service';
import { Doctor, Phc, Specialization } from '../../core/models/models';
import { DoctorFormDialogComponent } from './doctor-form-dialog.component';

@Component({
  selector: 'hg-doctor-admin',
  standalone: true,
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './doctor-admin.component.html',
  styleUrl: './doctor-admin.component.scss',
})
export class DoctorAdminComponent implements OnInit {
  private readonly service = inject(DoctorsService);
  private readonly reference = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);

  readonly columns = ['name', 'specialization', 'phc', 'slots', 'actions'];
  readonly doctors = signal<Doctor[]>([]);
  readonly phcs = signal<Phc[]>([]);
  readonly specializations = signal<Specialization[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    forkJoin({
      doctors: this.service.search(),
      phcs: this.reference.phcs(),
      specializations: this.reference.specializations(),
    }).subscribe({
      next: ({ doctors, phcs, specializations }) => {
        this.doctors.set(doctors);
        this.phcs.set(phcs);
        this.specializations.set(specializations);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  open(doctor: Doctor | null): void {
    this.dialog
      .open(DoctorFormDialogComponent, {
        data: { doctor, phcs: this.phcs(), specializations: this.specializations() },
      })
      .afterClosed()
      .subscribe((saved: Doctor | undefined) => {
        if (saved) {
          this.doctors.update((list) => {
            const idx = list.findIndex((d) => d.id === saved.id);
            return idx >= 0 ? list.map((d) => (d.id === saved.id ? saved : d)) : [...list, saved];
          });
        }
      });
  }

  remove(doctor: Doctor): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: { title: 'Remove doctor?', message: doctor.name, confirmText: 'Remove', danger: true },
      })
      .afterClosed()
      .subscribe((ok) => {
        if (ok) {
          this.service
            .remove(doctor.id)
            .subscribe(() => this.doctors.update((l) => l.filter((d) => d.id !== doctor.id)));
        }
      });
  }
}
