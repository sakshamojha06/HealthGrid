import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { debounceTime } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { VisitsService } from '../../core/services/visits.service';
import { ReferenceService } from '../../core/services/reference.service';
import { Disease, PatientVisit } from '../../core/models/models';

@Component({
  selector: 'hg-visit-list',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './visit-list.component.html',
  styleUrl: './visit-list.component.scss',
})
export class VisitListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly visits = inject(VisitsService);
  private readonly reference = inject(ReferenceService);

  readonly columns = ['visitDate', 'patient', 'diseases', 'medicines', 'doctor'];
  readonly rows = signal<PatientVisit[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly diseases = signal<Disease[]>([]);

  page = 0;
  pageSize = 20;

  readonly filters = this.fb.nonNullable.group({
    search: '',
    diseaseId: '',
    from: '',
    to: '',
  });

  ngOnInit(): void {
    this.reference.diseases().subscribe((d) => this.diseases.set(d));
    this.filters.valueChanges.pipe(debounceTime(300)).subscribe(() => {
      this.page = 0;
      this.load();
    });
    this.load();
  }

  onPage(e: PageEvent): void {
    this.page = e.pageIndex;
    this.pageSize = e.pageSize;
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    const f = this.filters.getRawValue();
    this.visits
      .list({ page: this.page + 1, pageSize: this.pageSize, ...f })
      .subscribe({
        next: (res) => {
          this.rows.set(res.items);
          this.total.set(res.totalCount);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Could not load visits.');
          this.loading.set(false);
        },
      });
  }
}
