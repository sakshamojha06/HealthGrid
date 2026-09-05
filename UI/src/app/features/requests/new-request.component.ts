import { Component, OnInit, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { MedicineRequestsService } from '../../core/services/medicine-requests.service';
import { ReferenceService } from '../../core/services/reference.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  CreateMedicineRequest,
  Medicine,
  Phc,
  RecommendedSource,
} from '../../core/models/models';

@Component({
  selector: 'hg-new-request',
  standalone: true,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
  ],
  templateUrl: './new-request.component.html',
  styleUrl: './new-request.component.scss',
})
export class NewRequestComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(MedicineRequestsService);
  private readonly reference = inject(ReferenceService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);

  readonly phcs = signal<Phc[]>([]);
  readonly medicines = signal<Medicine[]>([]);
  readonly recommendations = signal<RecommendedSource[]>([]);
  readonly saving = signal(false);

  readonly items = new FormArray<FormGroup>([]);
  readonly form = this.fb.nonNullable.group({
    destinationPhcId: ['', Validators.required],
    neededByDate: [''],
    notes: [''],
    items: this.items,
  });

  ngOnInit(): void {
    const myPhc = this.auth.user()?.phcId;
    this.reference.phcs().subscribe((p) => this.phcs.set(p.filter((x) => x.id !== myPhc)));
    this.reference.medicines({ pageSize: 200 }).subscribe((m) => this.medicines.set(m.items));
    this.addItem();
  }

  private newItem(): FormGroup {
    return this.fb.nonNullable.group({
      medicineId: ['', Validators.required],
      quantityRequested: [1, [Validators.required, Validators.min(0.001)]],
    });
  }

  addItem(): void {
    this.items.push(this.newItem());
  }

  removeItem(i: number): void {
    this.items.removeAt(i);
  }

  suggestSources(i: number): void {
    const g = this.items.at(i);
    const medicineId = g.get('medicineId')?.value;
    const qty = g.get('quantityRequested')?.value;
    if (!medicineId || !qty) {
      return;
    }
    this.service.recommendedSources(medicineId, qty).subscribe((r) => {
      this.recommendations.set(r);
    });
  }

  useSource(phcId: string): void {
    this.form.patchValue({ destinationPhcId: phcId });
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body: CreateMedicineRequest = {
      destinationPhcId: v.destinationPhcId,
      neededByDate: v.neededByDate || null,
      notes: v.notes || null,
      items: (v.items as { medicineId: string; quantityRequested: number }[]).filter(
        (x) => x.medicineId,
      ),
    };
    this.saving.set(true);
    this.service
      .create(body)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (r) => {
          this.snack.open('Request sent — the destination PHC has been notified.', 'OK', {
            duration: 4000,
          });
          this.router.navigate(['/requests', r.id]);
        },
      });
  }
}
