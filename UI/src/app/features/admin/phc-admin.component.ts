import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { ReferenceService } from '../../core/services/reference.service';
import { District, Phc } from '../../core/models/models';

@Component({
  selector: 'hg-phc-admin',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './phc-admin.component.html',
  styleUrl: './reference-crud.component.scss',
})
export class PhcAdminComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly ref = inject(ReferenceService);

  readonly columns = ['name', 'code', 'district', 'actions'];
  readonly rows = signal<Phc[]>([]);
  readonly districts = signal<District[]>([]);
  readonly loading = signal(true);
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    code: ['', Validators.required],
    districtId: ['', Validators.required],
  });

  ngOnInit(): void {
    forkJoin({ phcs: this.ref.phcs(), districts: this.ref.districts() }).subscribe({
      next: ({ phcs, districts }) => {
        this.rows.set(phcs);
        this.districts.set(districts);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  districtName(id: string): string {
    return this.districts().find((d) => d.id === id)?.name ?? '—';
  }

  startCreate(): void {
    this.editingId.set('new');
    this.form.reset();
  }

  startEdit(row: Phc): void {
    this.editingId.set(row.id);
    this.form.patchValue(row);
  }

  cancel(): void {
    this.editingId.set(null);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const body = this.form.getRawValue();
    const id = this.editingId();
    const call = id && id !== 'new' ? this.ref.updatePhc(id, body) : this.ref.createPhc(body);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.editingId.set(null);
        this.ref.phcs().subscribe((p) => this.rows.set(p));
      },
      error: () => this.saving.set(false),
    });
  }
}
