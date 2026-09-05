import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { DoctorsService } from '../../core/services/doctors.service';
import { ReferenceService } from '../../core/services/reference.service';
import { Doctor, Phc, Specialization } from '../../core/models/models';

const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

@Component({
  selector: 'hg-specialist-search',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatSelectModule,
    MatIconModule,
    MatChipsModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './specialist-search.component.html',
  styleUrl: './specialist-search.component.scss',
})
export class SpecialistSearchComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly doctors = inject(DoctorsService);
  private readonly reference = inject(ReferenceService);

  readonly days = DAYS;
  readonly specializations = signal<Specialization[]>([]);
  readonly phcs = signal<Phc[]>([]);
  readonly results = signal<Doctor[]>([]);
  readonly loading = signal(true);

  readonly filters = this.fb.nonNullable.group({
    specializationId: '',
    phcId: '',
    day: '',
  });

  ngOnInit(): void {
    this.reference.specializations().subscribe((s) => this.specializations.set(s));
    this.reference.phcs().subscribe((p) => this.phcs.set(p));
    this.filters.valueChanges.subscribe(() => this.search());
    this.search();
  }

  dayLabel(d: number): string {
    return DAYS[d] ?? '?';
  }

  private search(): void {
    this.loading.set(true);
    const f = this.filters.value;
    this.doctors
      .search({
        specializationId: f.specializationId || undefined,
        phcId: f.phcId || undefined,
        day: f.day ? Number(f.day) : undefined,
      })
      .subscribe({
        next: (d) => {
          this.results.set(d);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }
}
