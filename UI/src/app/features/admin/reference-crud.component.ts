import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';

export interface FieldDef {
  key: string;
  label: string;
  type?: 'text' | 'toggle';
  required?: boolean;
}

export interface ReferenceCrudConfig<T> {
  title: string;
  subtitle: string;
  fields: FieldDef[];
  list: () => Observable<T[]>;
  create: (body: Partial<T>) => Observable<T>;
  update: (id: string, body: Partial<T>) => Observable<T>;
}

/**
 * Generic inline create/edit table for the simple reference entities
 * (districts, diseases, specializations, medicines). Config-driven so each
 * admin screen is a five-line wrapper.
 */
@Component({
  selector: 'hg-reference-crud',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './reference-crud.component.html',
  styleUrl: './reference-crud.component.scss',
})
export class ReferenceCrudComponent<T extends { id: string }> implements OnInit {
  private readonly fb = inject(FormBuilder);

  /** Provided by the thin wrapper component for each admin route. */
  @Input({ required: true }) config!: ReferenceCrudConfig<T>;

  readonly rows = signal<T[]>([]);
  readonly loading = signal(true);
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);

  form!: FormGroup;
  columns: string[] = [];

  ngOnInit(): void {
    this.columns = [...this.config.fields.map((f) => f.key), 'actions'];
    this.buildForm();
    this.reload();
  }

  private buildForm(): void {
    const group: Record<string, unknown[]> = {};
    for (const f of this.config.fields) {
      group[f.key] = [f.type === 'toggle' ? true : '', f.required ? [Validators.required] : []];
    }
    this.form = this.fb.group(group);
  }

  reload(): void {
    this.loading.set(true);
    this.config.list().subscribe({
      next: (r) => {
        this.rows.set(r);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  startCreate(): void {
    this.editingId.set('new');
    this.form.reset();
    for (const f of this.config.fields) {
      if (f.type === 'toggle') {
        this.form.get(f.key)?.setValue(true);
      }
    }
  }

  startEdit(row: T): void {
    this.editingId.set(row.id);
    this.form.patchValue(row as Record<string, unknown>);
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
    const body = this.form.value as Partial<T>;
    const id = this.editingId();
    const call =
      id && id !== 'new' ? this.config.update(id, body) : this.config.create(body);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.editingId.set(null);
        this.reload();
      },
      error: () => this.saving.set(false),
    });
  }

  display(row: T, key: string): unknown {
    return (row as Record<string, unknown>)[key];
  }
}
